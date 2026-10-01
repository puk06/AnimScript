using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript の構文解析器。トークン列から AST を組み立てる。
    ///
    /// 文法（ゆるいBNF）:
    /// <code>
    /// script      := item*
    /// item        := paramDecl | layerDecl | stateDecl | transition | driverAction
    ///              | varDecl | assign | forLoop | whileLoop   （場所により使えるものが違う）
    /// paramDecl   := "param" IDENT ":" ("float"|"int"|"bool") ("=" expr)?
    /// layerDecl   := "layer" STRING layerOpt* block
    /// stateDecl   := "state" IDENT ("=" STRING)? stateOpt* block?
    /// transition  := node ("->" node)+ transOpt*
    /// varDecl     := "var" IDENT "=" expr
    /// assign      := IDENT "=" expr
    /// forLoop     := "for" IDENT "in" expr ".." expr ("step" expr)? block
    /// whileLoop   := "while" expr (比較演算子 expr)? block
    /// expr        := 数値 | $変数 | true | false | "(" expr ")" | 四則演算（% 含む）
    /// </code>
    ///
    /// 数値を書ける場所はすべて式（expr）を受け付ける。
    /// ループ・変数の展開は Parser ではなく AstExpander が行う。
    ///
    /// エラーが出てもその文を読み飛ばして解析を続ける（エラーをまとめて報告するため）。
    /// </summary>
    internal sealed class Parser
    {
        readonly IReadOnlyList<Token> _tokens;
        readonly DiagnosticBag _diagnostics;
        int _position;

        Parser(IReadOnlyList<Token> tokens, DiagnosticBag diagnostics)
        {
            _tokens = tokens;
            _diagnostics = diagnostics;
        }

        /// <summary>ソース文字列を解析して AST を返す。エラーは diagnostics に蓄積される。</summary>
        public static AnimScriptAst Parse(string source, DiagnosticBag diagnostics)
        {
            var tokens = Lexer.Tokenize(source, diagnostics);
            var parser = new Parser(tokens, diagnostics);
            return parser.ParseScript();
        }

        // ================================================================
        // トークン操作の基本
        // ================================================================

        Token Current => _tokens[System.Math.Min(_position, _tokens.Count - 1)];

        bool IsAtEnd => Current.Kind == TokenKind.EndOfFile;

        Token Advance()
        {
            var token = Current;
            if (!IsAtEnd) _position++;
            return token;
        }

        /// <summary>現在位置から n 先のトークンを覗く（代入文の先読みなどに使う）。</summary>
        Token Peek(int offset)
            => _tokens[System.Math.Min(_position + offset, _tokens.Count - 1)];

        bool CheckKeyword(string keyword)
            => Current.Kind == TokenKind.Identifier && Current.Text == keyword;

        Token Expect(TokenKind kind, string what)
        {
            if (Current.Kind == kind) return Advance();
            throw Error(Current, $"{what}が必要ですが、「{Current.Describe()}」が見つかりました");
        }

        ParseException Error(Token token, string message)
        {
            _diagnostics.Error(token.Location, message);
            return new ParseException(message);
        }

        void SkipNewlines()
        {
            while (Current.Kind == TokenKind.Newline) Advance();
        }

        /// <summary>
        /// エラー後の回復処理。次の文の先頭（改行の次）まで読み飛ばす。
        /// 「}」の手前では止まる（ブロックを閉じられるようにするため）。
        /// </summary>
        void SkipToNextStatement()
        {
            while (!IsAtEnd && Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.CloseBrace)
                Advance();
            if (Current.Kind == TokenKind.Newline) Advance();
        }

        /// <summary>文の終わり（改行・「}」・EOF のどれか）を要求する。</summary>
        void ExpectEndOfLine()
        {
            if (Current.Kind == TokenKind.Newline) { Advance(); return; }
            if (Current.Kind == TokenKind.CloseBrace || IsAtEnd) return;
            throw Error(Current, $"行の終わりが必要ですが、「{Current.Describe()}」が見つかりました");
        }

        /// <summary>ブロックの始まりを要求する。「{」は次の行に書いてもよい（Allman スタイル可）。</summary>
        void ExpectBlockStart()
        {
            if (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.OpenBrace && !IsAtEnd)
                throw Error(Current, $"「{{」か行の終わりが必要ですが、「{Current.Describe()}」が見つかりました");
            SkipNewlines();
        }

        // ================================================================
        // 文のディスパッチ（コンテキストごと）
        // ================================================================

        AnimScriptAst ParseScript()
        {
            var ast = new AnimScriptAst();

            while (!IsAtEnd)
            {
                SkipNewlines();
                if (IsAtEnd) break;

                // 対応する { の無い } はここで始末する（読み飛ばさないと無限ループになる）
                if (Current.Kind == TokenKind.CloseBrace)
                {
                    _diagnostics.Error(Current.Location, "対応する「{」の無い「}」です");
                    Advance();
                    continue;
                }

                try
                {
                    ast.Items.Add(ParseItem(ParseContext.Script));
                }
                catch (ParseException)
                {
                    SkipToNextStatement();
                }
            }

            return ast;
        }

        IBlockItem ParseItem(ParseContext context)
        {
            // どのコンテキストでも使えるもの
            if (CheckKeyword("var")) return ParseVarDecl();
            if (CheckKeyword("for")) return ParseFor(context);
            if (CheckKeyword("while")) return ParseWhile(context);

            switch (context)
            {
                case ParseContext.Script:
                    if (CheckKeyword("param")) return ParseParameter();
                    if (CheckKeyword("layer")) return ParseLayer();
                    break;

                case ParseContext.Layer:
                    if (CheckKeyword("state")) return ParseState();
                    break;

                case ParseContext.Driver:
                    break;
            }

            // 変数への代入（「名前 = 式」の形）
            if (Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Equals)
                return ParseAssign();

            // コンテキスト固有の文へ
            switch (context)
            {
                case ParseContext.Layer:
                    return ParseTransition();
                case ParseContext.Driver:
                    return ParseDriverAction();
                default:
                    throw Error(Current, $"ここでは param / layer / var / for / while が使えます。「{Current.Describe()}」は使えません");
            }
        }

        /// <summary>{ ... } の中身を読む。ループの本体などに使う共通部品。</summary>
        void ParseItemBlock(ParseContext context, List<IBlockItem> items)
        {
            Expect(TokenKind.OpenBrace, "「{」");

            while (!IsAtEnd && Current.Kind != TokenKind.CloseBrace)
            {
                SkipNewlines();
                if (IsAtEnd || Current.Kind == TokenKind.CloseBrace) break;

                try
                {
                    items.Add(ParseItem(context));
                }
                catch (ParseException)
                {
                    SkipToNextStatement();
                }
            }

            Expect(TokenKind.CloseBrace, "「}」（ブロックの閉じ括弧）");
        }

        // ================================================================
        // ビルド時変数・ループ
        // ================================================================

        VarDeclAst ParseVarDecl()
        {
            var keyword = Advance(); // "var"
            var name = Expect(TokenKind.Identifier, "変数名");
            CheckPlainVariableName(name);

            Expect(TokenKind.Equals, "「=」");
            var value = ParseExpression();
            ExpectEndOfLine();

            return new VarDeclAst
            {
                Name = name.Text,
                Value = value,
                Location = keyword.Location,
            };
        }

        AssignAst ParseAssign()
        {
            var name = Advance(); // 識別子
            CheckPlainVariableName(name);

            Expect(TokenKind.Equals, "「=」");
            var value = ParseExpression();
            ExpectEndOfLine();

            return new AssignAst
            {
                Name = name.Text,
                Value = value,
                Location = name.Location,
            };
        }

        /// <summary>宣言・代入の変数名には $ を付けない、のチェック。</summary>
        void CheckPlainVariableName(Token name)
        {
            if (name.Text.Contains('$'))
                throw Error(name, "変数の宣言・代入では $ を付けずに書いてください（参照するときだけ「$i」のように書きます）");
        }

        LoopAst ParseFor(ParseContext context)
        {
            var keyword = Advance(); // "for"
            var varToken = Expect(TokenKind.Identifier, "ループ変数名");
            CheckPlainVariableName(varToken);

            if (!CheckKeyword("in"))
                throw Error(Current, "for には in が必要です（例: for i in 0..7）");
            Advance();

            var from = ParseExpression();
            Expect(TokenKind.DotDot, "「..」");
            var to = ParseExpression();

            ExprAst step = null;
            if (CheckKeyword("step"))
            {
                Advance();
                step = ParseExpression();
            }

            ExpectBlockStart();

            var loop = new LoopAst
            {
                Kind = LoopKind.For,
                VarName = varToken.Text,
                From = from,
                To = to,
                Step = step,
                Location = keyword.Location,
            };
            ParseItemBlock(context, loop.Body);
            return loop;
        }

        LoopAst ParseWhile(ParseContext context)
        {
            var keyword = Advance(); // "while"
            var condition = ParseWhileCondition();

            ExpectBlockStart();

            var loop = new LoopAst
            {
                Kind = LoopKind.While,
                Condition = condition,
                Location = keyword.Location,
            };
            ParseItemBlock(context, loop.Body);
            return loop;
        }

        /// <summary>
        /// while の条件。ビルド時に自分たちで評価するので >= / <= も使える。
        /// 比較演算子が無ければ「0 以外なら真」。
        /// </summary>
        WhileCondAst ParseWhileCondition()
        {
            var left = ParseExpression();

            CmpOp? op = null;
            switch (Current.Kind)
            {
                case TokenKind.EqualsEquals: op = CmpOp.Equals; break;
                case TokenKind.BangEquals:   op = CmpOp.NotEquals; break;
                case TokenKind.Less:         op = CmpOp.Less; break;
                case TokenKind.Greater:      op = CmpOp.Greater; break;
                case TokenKind.LessEqual:    op = CmpOp.LessEqual; break;
                case TokenKind.GreaterEqual: op = CmpOp.GreaterEqual; break;
            }

            ExprAst right = null;
            if (op.HasValue)
            {
                Advance();
                right = ParseExpression();
            }

            return new WhileCondAst
            {
                Left = left,
                Op = op,
                Right = right,
                Location = left.Location,
            };
        }

        // ================================================================
        // param
        // ================================================================

        ParameterAst ParseParameter()
        {
            var keyword = Advance(); // "param"

            var nameToken = Expect(TokenKind.Identifier, "パラメータ名");
            Expect(TokenKind.Colon, "「:」");

            var typeToken = Expect(TokenKind.Identifier, "型（float / int / bool）");
            ParamKind kind;
            switch (typeToken.Text)
            {
                case "float": kind = ParamKind.Float; break;
                case "int":   kind = ParamKind.Int;   break;
                case "bool":  kind = ParamKind.Bool;  break;
                default:
                    throw Error(typeToken, $"不明な型「{typeToken.Text}」です。float / int / bool のいずれかを指定してください");
            }

            ExprAst defaultValue = null;
            if (Current.Kind == TokenKind.Equals)
            {
                Advance();
                defaultValue = ParseExpression();
            }

            ExpectEndOfLine();

            return new ParameterAst
            {
                Name = nameToken.Text,
                Kind = kind,
                DefaultValue = defaultValue,
                Location = nameToken.Location,
            };
        }

        // ================================================================
        // layer
        // ================================================================

        LayerAst ParseLayer()
        {
            var layerKeyword = Advance(); // "layer"
            var nameToken = Expect(TokenKind.String, "レイヤー名（\"…\"）");

            var layer = new LayerAst
            {
                Name = nameToken.Text,
                Location = layerKeyword.Location,
            };

            // レイヤーオプション（layer の行にだけ書ける）
            while (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.OpenBrace && !IsAtEnd)
            {
                if (CheckKeyword("default")) { Advance(); layer.IsDefault = true; }
                else if (CheckKeyword("wd") || CheckKeyword("writeDefault")) { var option = Advance(); layer.WriteDefaults = ParseOnOff(option.Text); }
                else if (CheckKeyword("loop")) { Advance(); layer.Loop = ParseOnOff("loop"); }
                else if (CheckKeyword("weight")) { Advance(); layer.Weight = ParseExpression(); }
                else throw Error(Current, $"不明なレイヤーオプション「{Current.Describe()}」です。default / wd（writeDefault） / loop / weight が使えます");
            }

            ExpectBlockStart();
            ParseItemBlock(ParseContext.Layer, layer.Items);
            return layer;
        }

        // ================================================================
        // state
        // ================================================================

        StateAst ParseState()
        {
            var stateKeyword = Advance(); // "state"
            var nameToken = Expect(TokenKind.Identifier, "ステート名");

            var state = new StateAst
            {
                Name = nameToken.Text,
                Location = stateKeyword.Location,
            };

            // = "クリップ" の指定
            if (Current.Kind == TokenKind.Equals)
            {
                Advance();
                state.ClipPath = Expect(TokenKind.String, "クリップのパス（\"…\"）").Text;
                state.HasClip = true;
            }

            // インラインオプション（state の行にだけ書ける）
            while (Current.Kind != TokenKind.Newline
                   && Current.Kind != TokenKind.OpenBrace
                   && Current.Kind != TokenKind.CloseBrace
                   && !IsAtEnd)
            {
                if (CheckKeyword("speed")) { Advance(); state.Speed = ParseExpression(); }
                else if (CheckKeyword("cycleOffset") || CheckKeyword("co")) { Advance(); state.CycleOffset = ParseExpression(); }
                else if (CheckKeyword("time")) { Advance(); state.TimeParameter = Expect(TokenKind.Identifier, "Motion Time 用パラメータ名").Text; }
                else if (CheckKeyword("wd") || CheckKeyword("writeDefault")) { var option = Advance(); state.WriteDefaults = ParseOnOff(option.Text); }
                else if (CheckKeyword("loop")) { Advance(); state.Loop = ParseOnOff("loop"); }
                else if (CheckKeyword("mirror")) { Advance(); state.Mirror = ParseOnOff("mirror"); }
                else if (CheckKeyword("footIK")) { Advance(); state.FootIK = ParseOnOff("footIK"); }
                else throw Error(Current, $"不明なステートオプション「{Current.Describe()}」です。speed / cycleOffset（co） / time / wd（writeDefault） / loop / mirror / footIK が使えます");
            }

            // ブロック書き（{ は次の行でもOK）
            SkipNewlines();
            if (Current.Kind == TokenKind.OpenBrace)
                ParseStateBlock(state);

            return state;
        }

        void ParseStateBlock(StateAst state)
        {
            Expect(TokenKind.OpenBrace, "「{」");

            while (!IsAtEnd && Current.Kind != TokenKind.CloseBrace)
            {
                SkipNewlines();
                if (IsAtEnd || Current.Kind == TokenKind.CloseBrace) break;

                try
                {
                    if (CheckKeyword("clip"))
                    {
                        Advance();
                        ParseClip(state);
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("speed"))
                    {
                        Advance();
                        state.Speed = ParseExpression();
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("cycleOffset") || CheckKeyword("co"))
                    {
                        Advance();
                        state.CycleOffset = ParseExpression();
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("time"))
                    {
                        Advance();
                        state.TimeParameter = Expect(TokenKind.Identifier, "Motion Time 用パラメータ名").Text;
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("wd") || CheckKeyword("writeDefault"))
                    {
                        var option = Advance();
                        state.WriteDefaults = ParseOnOff(option.Text);
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("loop"))
                    {
                        Advance();
                        state.Loop = ParseOnOff("loop");
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("mirror"))
                    {
                        Advance();
                        state.Mirror = ParseOnOff("mirror");
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("footIK"))
                    {
                        Advance();
                        state.FootIK = ParseOnOff("footIK");
                        ExpectEndOfLine();
                    }
                    else if (CheckKeyword("driver"))
                    {
                        state.Driver = ParseDriverBlock();
                    }
                    else
                    {
                        throw Error(Current, $"ステート内では clip / speed / cycleOffset（co） / time / wd（writeDefault） / loop / mirror / footIK / driver が使えます。「{Current.Describe()}」は使えません");
                    }
                }
                catch (ParseException)
                {
                    SkipToNextStatement();
                }
            }

            Expect(TokenKind.CloseBrace, "「}」（ステートの閉じ括弧）");
        }

        void ParseClip(StateAst state)
        {
            if (CheckKeyword("none"))
            {
                Advance();
                state.HasClip = true;
                state.ClipPath = null; // 明示的な「motion 無し」
                return;
            }

            state.ClipPath = Expect(TokenKind.String, "クリップのパス（\"…\"）または none").Text;
            state.HasClip = true;
        }

        // ================================================================
        // driver（VRC Parameter Driver）
        // ================================================================

        DriverBlockAst ParseDriverBlock()
        {
            var driverKeyword = Advance(); // "driver"

            var block = new DriverBlockAst
            {
                Location = driverKeyword.Location,
            };

            if (CheckKeyword("localOnly"))
            {
                Advance();
                block.LocalOnly = true;
            }

            ExpectBlockStart();
            ParseItemBlock(ParseContext.Driver, block.Items);
            return block;
        }

        DriverActionAst ParseDriverAction()
        {
            var keyword = Expect(TokenKind.Identifier, "set / add / random / copy");

            switch (keyword.Text)
            {
                case "set":
                case "add":
                {
                    var target = Expect(TokenKind.Identifier, "パラメータ名");
                    var value = ParseExpression();
                    return new DriverActionAst
                    {
                        Kind = keyword.Text == "set" ? DriverActionKind.Set : DriverActionKind.Add,
                        Target = target.Text,
                        Value = value,
                        Location = keyword.Location,
                    };
                }
                case "random":
                {
                    var target = Expect(TokenKind.Identifier, "パラメータ名");
                    var min = ParseExpression();
                    var max = ParseExpression();
                    return new DriverActionAst
                    {
                        Kind = DriverActionKind.Random,
                        Target = target.Text,
                        ValueMin = min,
                        ValueMax = max,
                        Location = keyword.Location,
                    };
                }
                case "copy":
                {
                    var source = Expect(TokenKind.Identifier, "コピー元のパラメータ名");
                    Expect(TokenKind.Arrow, "「->」");
                    var destination = Expect(TokenKind.Identifier, "コピー先のパラメータ名");
                    return new DriverActionAst
                    {
                        Kind = DriverActionKind.Copy,
                        Source = source.Text,
                        Target = destination.Text,
                        Location = keyword.Location,
                    };
                }
                default:
                    throw Error(keyword, $"不明な driver コマンド「{keyword.Text}」です。set / add / random / copy が使えます");
            }
        }

        // ================================================================
        // 遷移
        // ================================================================

        TransitionAst ParseTransition()
        {
            var first = Expect(TokenKind.Identifier, "ステート名 / entry / exit / any");

            var transition = new TransitionAst
            {
                Location = first.Location,
            };
            transition.Chain.Add(first.Text);

            // A -> B -> C ... のチェインを読む
            while (Current.Kind == TokenKind.Arrow)
            {
                Advance();
                var node = Expect(TokenKind.Identifier, "遷移先のステート名");
                transition.Chain.Add(node.Text);
            }

            if (transition.Chain.Count < 2)
                throw Error(first, "遷移は「A -> B」のように -> でつないでください");

            // 遷移オプション
            while (Current.Kind != TokenKind.Newline
                   && Current.Kind != TokenKind.CloseBrace
                   && !IsAtEnd)
            {
                if (CheckKeyword("when"))
                {
                    Advance();
                    ParseConditions(transition);
                }
                else if (CheckKeyword("exitTime"))
                {
                    var keyword = Advance();
                    if (transition.ExitTime != null)
                        throw Error(keyword, "exitTime は1回だけ指定できます");
                    transition.ExitTime = ParseExpression();
                }
                else if (CheckKeyword("dur"))
                {
                    var keyword = Advance();
                    if (transition.Duration != null)
                        throw Error(keyword, "dur は1回だけ指定できます");
                    transition.Duration = ParseExpression();
                }
                else if (CheckKeyword("offset"))
                {
                    var keyword = Advance();
                    if (transition.Offset != null)
                        throw Error(keyword, "offset は1回だけ指定できます");
                    transition.Offset = ParseExpression();
                }
                else if (CheckKeyword("self"))
                {
                    Advance();
                    transition.AllowSelf = true;
                }
                else
                {
                    throw Error(Current, $"不明な遷移オプション「{Current.Describe()}」です。when / exitTime / dur / offset / self が使えます");
                }
            }

            ExpectEndOfLine();
            return transition;
        }

        void ParseConditions(TransitionAst transition)
        {
            if (transition.Condition != null)
                throw Error(Current, "when は1回だけ指定できます（条件を増やすには and / or でつないでください）");

            transition.Condition = ParseConditionOr();
        }

        // --- 条件式（or / and / 括弧） ---
        // 優先順位: or < and < 項。括弧で明示もできる。

        ConditionExprAst ParseConditionOr()
        {
            var left = ParseConditionAnd();
            while (CheckKeyword("or"))
            {
                var keyword = Advance();
                var right = ParseConditionAnd();
                left = new ConditionBinaryAst
                {
                    IsOr = true,
                    Left = left,
                    Right = right,
                    Location = keyword.Location,
                };
            }
            return left;
        }

        ConditionExprAst ParseConditionAnd()
        {
            var left = ParseConditionPrimary();
            while (CheckKeyword("and"))
            {
                var keyword = Advance();
                var right = ParseConditionPrimary();
                left = new ConditionBinaryAst
                {
                    IsOr = false,
                    Left = left,
                    Right = right,
                    Location = keyword.Location,
                };
            }
            return left;
        }

        ConditionExprAst ParseConditionPrimary()
        {
            // 括弧: (A or B) and C のような結合
            if (Current.Kind == TokenKind.OpenParen)
            {
                Advance();
                var inner = ParseConditionOr();
                Expect(TokenKind.CloseParen, "「)」");
                return inner;
            }

            // !Param （false のとき）
            if (Current.Kind == TokenKind.Bang)
            {
                var bang = Advance();

                // !(A or B) の形は表現できない（Unity に >= / <= が無く、否定が作れないため）
                if (Current.Kind == TokenKind.OpenParen)
                    throw Error(bang, "「!(...)」の形は使えません。条件を分解して書いてください（例: !(A or B) → !A and !B）");

                var notParam = Expect(TokenKind.Identifier, "パラメータ名");
                return new ConditionAst
                {
                    Param = notParam.Text,
                    Op = CondOp.IfNot,
                    Location = bang.Location,
                };
            }

            var param = Expect(TokenKind.Identifier, "パラメータ名");

            switch (Current.Kind)
            {
                case TokenKind.Greater:      Advance(); return MakeComparison(param, CondOp.Greater);
                case TokenKind.Less:         Advance(); return MakeComparison(param, CondOp.Less);
                case TokenKind.EqualsEquals: Advance(); return MakeComparison(param, CondOp.Equals);
                case TokenKind.BangEquals:   Advance(); return MakeComparison(param, CondOp.NotEquals);

                // Unity の AnimatorCondition には >= / <= が無いので、分かりやすいエラーにする
                case TokenKind.GreaterEqual:
                    throw Error(Current, "「>=」は遷移条件には使えません。「>」（より大きい）で書き直してください");
                case TokenKind.LessEqual:
                    throw Error(Current, "「<=」は遷移条件には使えません。「<」（より小さい）で書き直してください");

                // 演算子が無ければ「Param が true のとき」
                default:
                    return new ConditionAst
                    {
                        Param = param.Text,
                        Op = CondOp.If,
                        Location = param.Location,
                    };
            }
        }

        ConditionAst MakeComparison(Token param, CondOp op)
        {
            return new ConditionAst
            {
                Param = param.Text,
                Op = op,
                Value = ParseExpression(),
                Location = param.Location,
            };
        }

        // ================================================================
        // 式（数値・$変数・四則演算）
        // ================================================================

        /// <summary>式を解析する。数値を書ける場所ではすべてこれを使う。</summary>
        ExprAst ParseExpression() => ParseAdditive();

        ExprAst ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Current.Kind == TokenKind.Plus || Current.Kind == TokenKind.Minus)
            {
                var opToken = Advance();
                var right = ParseMultiplicative();
                var op = opToken.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
                left = new BinaryExpr(left, op, right, opToken.Location);
            }
            return left;
        }

        ExprAst ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Current.Kind == TokenKind.Star
                   || Current.Kind == TokenKind.Slash
                   || Current.Kind == TokenKind.Percent)
            {
                var opToken = Advance();
                var right = ParseUnary();
                BinaryOp op;
                switch (opToken.Kind)
                {
                    case TokenKind.Star:    op = BinaryOp.Mul; break;
                    case TokenKind.Slash:   op = BinaryOp.Div; break;
                    default:                op = BinaryOp.Mod; break;
                }
                left = new BinaryExpr(left, op, right, opToken.Location);
            }
            return left;
        }

        ExprAst ParseUnary()
        {
            // 負数は「0 - x」として表現する
            if (Current.Kind == TokenKind.Minus)
            {
                var minus = Advance();
                var operand = ParseUnary();
                return new BinaryExpr(new ConstExpr(0, minus.Location), BinaryOp.Sub, operand, minus.Location);
            }
            return ParsePrimary();
        }

        ExprAst ParsePrimary()
        {
            if (Current.Kind == TokenKind.Number)
            {
                var number = Advance();
                return new ConstExpr(number.NumberValue, number.Location);
            }

            if (CheckKeyword("true"))
            {
                var keyword = Advance();
                return new ConstExpr(1, keyword.Location);
            }
            if (CheckKeyword("false"))
            {
                var keyword = Advance();
                return new ConstExpr(0, keyword.Location);
            }

            // $変数参照
            if (Current.Kind == TokenKind.Identifier && Current.Text.StartsWith('$'))
            {
                var variable = Advance();
                var name = variable.Text.Substring(1);
                if (name.Length == 0 || name.Contains('$'))
                    throw Error(variable, "式の中の変数は「$i」のように1つだけ書いてください");
                return new VarExpr(name, variable.Location);
            }

            if (Current.Kind == TokenKind.OpenParen)
            {
                Advance();
                var inner = ParseExpression();
                Expect(TokenKind.CloseParen, "「)」");
                return inner;
            }

            throw Error(Current, $"数値または $変数 が必要ですが、「{Current.Describe()}」が見つかりました");
        }

        // ================================================================
        // 共通の小さな構文
        // ================================================================

        bool ParseOnOff(string optionName)
        {
            if (CheckKeyword("on")) { Advance(); return true; }
            if (CheckKeyword("off")) { Advance(); return false; }
            throw Error(Current, $"{optionName} には on または off を指定してください");
        }
    }
}

using System.Globalization;

namespace CohEngine.Core.Expressions;

/// <summary>
/// Parses the Python expression grammar the Python port hands to <c>ast.parse</c>, as far as the magnitude
/// expressions reach: numbers, names, attributes, calls, unary signs, arithmetic, powers and comparisons.
/// The evaluator refuses every node it does not port, as the Python does.
/// </summary>
internal sealed class Parser
{
    private static readonly string[] Comparisons = ["==", "!=", "<=", ">=", "<", ">"];
    private static readonly string[] Symbols = ["**", "//", "==", "!=", "<=", ">=", "<", ">", "+", "-", "*", "/", "%", "(", ")", ",", ".", "~"];

    private readonly List<string> tokens;
    private int position;

    private Parser(List<string> tokens)
    {
        this.tokens = tokens;
    }

    private string? Peek => position < tokens.Count ? tokens[position] : null;

    /// <summary>Parses an expression.</summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The tree, or null for text that is not an expression.</returns>
    public static Node? Parse(string text)
    {
        List<string>? lexed = Tokenize(text);
        if (lexed is null || lexed.Count == 0)
        {
            return null;
        }

        Parser parser = new(lexed);
        Node? node = parser.Comparison();
        return node is not null && parser.position == lexed.Count ? node : null;
    }

    private static List<string>? Tokenize(string text)
    {
        List<string> found = [];
        int i = 0;
        while (i < text.Length)
        {
            char current = text[i];
            int start = i;
            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            if (char.IsAsciiDigit(current) || (current == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])))
            {
                i = ScanNumber(text, i);
            }
            else if (char.IsLetter(current) || current == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }
            }
            else
            {
                string? symbol = Symbols.FirstOrDefault(candidate => string.CompareOrdinal(text, start, candidate, 0, candidate.Length) == 0);
                if (symbol is null)
                {
                    return null;
                }

                i += symbol.Length;
            }

            found.Add(text[start..i]);
        }

        return found;
    }

    private static int ScanNumber(string text, int i)
    {
        while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.'))
        {
            i++;
        }

        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            int exponent = i + 1;
            if (exponent < text.Length && (text[exponent] == '+' || text[exponent] == '-'))
            {
                exponent++;
            }

            if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
            {
                i = exponent;
                while (i < text.Length && char.IsAsciiDigit(text[i]))
                {
                    i++;
                }
            }
        }

        return i;
    }

    private static bool IsNumber(string token)
    {
        return char.IsAsciiDigit(token[0]) || token[0] == '.';
    }

    private static bool IsName(string token)
    {
        return char.IsLetter(token[0]) || token[0] == '_';
    }

    private bool Accept(string token)
    {
        if (string.Equals(Peek, token, StringComparison.Ordinal))
        {
            position++;
            return true;
        }

        return false;
    }

    private Node? Comparison()
    {
        Node? left = Arithmetic();
        while (left is not null && Peek is { } symbol && Comparisons.Contains(symbol, StringComparer.Ordinal))
        {
            position++;
            Node? right = Arithmetic();
            left = right is null ? null : new Node.Binary(left, symbol, right);
        }

        return left;
    }

    private Node? Arithmetic()
    {
        Node? left = Term();
        while (left is not null && Peek is "+" or "-")
        {
            string symbol = tokens[position++];
            Node? right = Term();
            left = right is null ? null : new Node.Binary(left, symbol, right);
        }

        return left;
    }

    private Node? Term()
    {
        Node? left = Factor();
        while (left is not null && Peek is "*" or "/" or "//" or "%")
        {
            string symbol = tokens[position++];
            Node? right = Factor();
            left = right is null ? null : new Node.Binary(left, symbol, right);
        }

        return left;
    }

    private Node? Factor()
    {
        if (Peek is "+" or "-" or "~")
        {
            string symbol = tokens[position++];
            Node? operand = Factor();
            return operand is null ? null : new Node.Unary(symbol, operand);
        }

        Node? primary = Primary();
        if (primary is not null && Accept("**"))
        {
            Node? exponent = Factor();
            return exponent is null ? null : new Node.Binary(primary, "**", exponent);
        }

        return primary;
    }

    private Node? Primary()
    {
        Node? node = Atom();
        while (node is not null)
        {
            if (Accept("("))
            {
                List<Node> args = [];
                while (!Accept(")"))
                {
                    if ((args.Count > 0 && !Accept(",")) || Comparison() is not { } arg)
                    {
                        return null;
                    }

                    args.Add(arg);
                }

                node = new Node.Call(node, args);
            }
            else if (Accept("."))
            {
                node = Peek is { } attr && IsName(attr) ? new Node.Member(node, tokens[position++]) : null;
            }
            else
            {
                break;
            }
        }

        return node;
    }

    private Node? Atom()
    {
        if (Peek is not { } token)
        {
            return null;
        }

        if (Accept("("))
        {
            Node? inner = Comparison();
            return inner is not null && Accept(")") ? inner : null;
        }

        position++;
        if (IsNumber(token))
        {
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? new Node.Number(value, token) : null;
        }

        return IsName(token) ? new Node.Name(token) : null;
    }

    /// <summary>One node of a parsed expression; <see cref="object.ToString"/> unparses it as <c>ast.unparse</c> does.</summary>
    internal abstract record Node
    {
        /// <summary>A numeric literal.</summary>
        internal sealed record Number(double Value, string Text) : Node
        {
            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>A bare name.</summary>
        internal sealed record Name(string Identifier) : Node
        {
            public override string ToString()
            {
                return Identifier;
            }
        }

        /// <summary>An attribute access.</summary>
        internal sealed record Member(Node Value, string Attr) : Node
        {
            public override string ToString()
            {
                return $"{Value}.{Attr}";
            }
        }

        /// <summary>A binary operation, comparison included.</summary>
        internal sealed record Binary(Node Left, string Operator, Node Right) : Node
        {
            public override string ToString()
            {
                return $"{Left} {Operator} {Right}";
            }
        }

        /// <summary>A unary operation.</summary>
        internal sealed record Unary(string Operator, Node Operand) : Node
        {
            public override string ToString()
            {
                return Operator + Operand;
            }
        }

        /// <summary>A call.</summary>
        internal sealed record Call(Node Function, IReadOnlyList<Node> Args) : Node
        {
            public override string ToString()
            {
                return $"{Function}({string.Join(", ", Args)})";
            }
        }
    }
}

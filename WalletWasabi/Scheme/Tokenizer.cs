using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Scheme;

public static class Tokenizer
{
    public abstract record Token;
    public record OpenToken : Token;
    public record CloseToken : Token;
    public record QuoteToken : Token;
    public record QuasiQuoteToken : Token;
    public record UnquoteToken : Token;
    public record UnquoteSplicingToken : Token;
    public record DotToken : Token;
    public record NumberToken(string number) : Token;
    public record BooleanToken(bool b) : Token;
    public record CharacterToken(string c) : Token;
    public record StringToken(string str) : Token;
    public record SymbolToken(string symbol) : Token;

    private static readonly OpenToken Open = new();
    private static readonly CloseToken Close = new();
    private static readonly DotToken Dot = new();
    private static readonly QuoteToken Quote = new();
    private static readonly QuasiQuoteToken QuasiQuote = new();
    private static readonly UnquoteToken Unquote = new();
    private static readonly UnquoteSplicingToken UnquoteSplicing = new();
    private static readonly BooleanToken True = new(true);
    private static readonly BooleanToken False = new(false);

    public static IEnumerable<Token> Tokenize(string source)
    {
        var chars = source.ToCharArray();
        var i = 0;

        while (i < chars.Length)
        {
            var current = chars[i];
            // Skip whitespace
            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            // Skip comments
            if (current == ';')
            {
                ConsumeComment();
                continue;
            }

            switch(current)
            {
                case '(':
                    i++;
                    yield return Open;
                    break;
                case ')':
                    i++;
                    yield return Close;
                    break;
                case '\'':
                    i++;
                    yield return Quote;
                    break;
                case '`' :
                    i++;
                    yield return QuasiQuote;
                    break;
                case ',' when i + 1 < chars.Length && chars[i + 1] == '@':
                    i += 2;
                    yield return UnquoteSplicing;
                    break;
                case ',':
                    i++;
                    yield return Unquote;
                    break;
                case '.' when i + 1 < chars.Length && char.IsWhiteSpace(chars[i + 1]):
                    i++;
                    yield return Dot;
                    break;
                case '#' when  i + 1 < chars.Length && chars[i + 1] == '\\':
                    i += 2;
                    yield return new CharacterToken(ParseToken());
                    break;
                case '"':
                    yield return new StringToken(ParseString());
                    break;
                case '-' when i + 1 < chars.Length && char.IsDigit(chars[i + 1]):
                case '+' when i + 1 < chars.Length && char.IsDigit(chars[i + 1]):
                case '.' when i + 1 < chars.Length && char.IsDigit(chars[i + 1]):
                    yield return new NumberToken(ParseToken());
                    break;
                default:
                    yield return ParseToken()
                        .Then<string, Token>(t => t switch
                        {
                            "#t" or "#true" => True,
                            "#f" or "#false"  => False,
                            [var c, .. _] when char.IsDigit(c) => new NumberToken(t),
                            var symbol => new SymbolToken(symbol)
                        });
                    break;
            }
        }

        yield break;

        void ConsumeComment()
        {
            while (i < chars.Length && chars[i] != '\r' && chars[i] != '\n')
            {
                i++;
            }
        }

        string ParseString()
        {
            var sb = new StringBuilder();
            i++;
            while (i < chars.Length)
            {
                if (i + 1 < chars.Length && chars[i] == '\\')
                {
                    // Handle escape sequences with switch expression
                    var escaped = chars[i + 1] switch
                    {
                        '"' => '"',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        _ => throw new InvalidOperationException("Malformed string: invalid escape sequence")
                    };
                    sb.Append(escaped);
                    i += 2;
                }
                else if (chars[i] == '"')
                {
                    // Closing quote found
                    i++;
                    return sb.ToString();
                }
                else
                {
                    // Regular character
                    sb.Append(chars[i]);
                    i++;
                }
            }

            throw new InvalidOperationException("Malformed string: unterminated string literal");
        }

        string ParseToken()
        {
            var sb = new StringBuilder();

            while (i < chars.Length)
            {
                char current = chars[i];

                // Token terminates on closing paren, whitespace, or EOF
                if (current == ')' || char.IsWhiteSpace(current))
                {
                    break;
                }

                sb.Append(current);
                i++;
            }

            return sb.ToString();
        }
    }
}

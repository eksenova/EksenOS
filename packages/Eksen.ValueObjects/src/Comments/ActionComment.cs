namespace Eksen.ValueObjects.Comments;

public sealed record ActionComment : ValueObject<ActionComment, string>,
    IValueObjectParser<ActionComment, string>
{
    public const int MaxLength = 2000;

    private ActionComment(string value) : base(value)
    {
    }

    public static ActionComment Empty
    {
        get { return new ActionComment(string.Empty); }
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        value = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

        if (value.Length > MaxLength)
        {
            throw CommentErrors.ActionCommentTooLong.Raise(value, MaxLength);
        }

        return value;
    }

    public static ActionComment Create(string value)
    {
        return Parse(value);
    }

    public static ActionComment Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new ActionComment(value);
    }
}

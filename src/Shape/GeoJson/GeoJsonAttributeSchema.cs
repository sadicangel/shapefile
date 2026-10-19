using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DBase;

namespace Shape.GeoJson;

// A collection is inspected before creating storage: all rows share one ordered
// schema, even when members appear in different orders or are absent in some rows.
internal sealed class GeoJsonAttributeSchema
{
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    private readonly List<Column> _columns = [];

    public ImmutableArray<DbfFieldDescriptor> Descriptors { get; private set; }

    public GeoJsonAttributeSchema(IEnumerable<JsonElement> properties)
    {
        foreach (var row in properties)
        {
            if (row.ValueKind == JsonValueKind.Null) continue;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in row.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}'.");
                if (!_indices.TryGetValue(property.Name, out var index))
                {
                    index = _columns.Count;
                    _indices.Add(property.Name, index);
                    _columns.Add(new Column());
                }
                _columns[index].Include(property.Value);
            }
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptors = ImmutableArray.CreateBuilder<DbfFieldDescriptor>(_columns.Count);
        foreach (var name in _indices.Keys)
            descriptors.Add(_columns[_indices[name]].CreateDescriptor(CreateName(name, usedNames)));
        Descriptors = descriptors.MoveToImmutable();
        if (33L + Descriptors.Length * 32L > ushort.MaxValue ||
            1L + Descriptors.Sum(descriptor => (long)descriptor.Length) > ushort.MaxValue)
            throw new JsonException("The inferred properties exceed DBF header or record size limits.");
    }

    public DbfRecord CreateRecord(JsonElement properties)
    {
        var fields = new DbfField[_columns.Count];
        if (properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                var index = _indices[property.Name];
                fields[index] = _columns[index].Convert(property.Value);
            }
        }
        return new DbfRecord(Descriptors, [.. fields]);
    }

    private static string CreateName(string source, HashSet<string> usedNames)
    {
        var normalized = new string(source.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_').Take(10).ToArray());
        if (normalized.Length == 0) normalized = "Field";
        var name = normalized;
        for (var suffix = 2; !usedNames.Add(name); suffix++)
        {
            var ending = "_" + suffix.ToString(CultureInfo.InvariantCulture);
            name = normalized[..Math.Min(normalized.Length, 10 - ending.Length)] + ending;
        }
        return name;
    }

    private enum Kind { Unknown, Text, Number, Logical }

    private sealed class Column
    {
        private Kind _kind;
        private int _textLength = 1;
        private int _scale;
        private int _integerWidth = 1;
        private bool _useMemo;

        public void Include(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null) return;
            var textValue = Text(value);
            _textLength = Math.Max(_textLength, Encoding.UTF8.GetByteCount(textValue));
            _useMemo |= textValue.Length > 0 &&
                (char.IsWhiteSpace(textValue[0]) || char.IsWhiteSpace(textValue[^1]) || textValue.Contains('\0'));
            var kind = value.ValueKind switch
            {
                JsonValueKind.Number => Kind.Number,
                JsonValueKind.True or JsonValueKind.False => Kind.Logical,
                _ => Kind.Text
            };
            _kind = _kind == Kind.Unknown ? kind : _kind == kind ? kind : Kind.Text;
            if (kind != Kind.Number) return;

            var number = Number(value);
            var text = number.ToString("0.############################", CultureInfo.InvariantCulture);
            var dot = text.IndexOf('.');
            _scale = Math.Max(_scale, dot < 0 ? 0 : text.Length - dot - 1);
            _integerWidth = Math.Max(_integerWidth, dot < 0 ? text.Length : dot);
            // Fractional Numeric values pass through Double in DBase. Account for a
            // conversion that rounds across an integer boundary before choosing the layout.
            _integerWidth = Math.Max(_integerWidth,
                Math.Truncate((double)number).ToString("0", CultureInfo.InvariantCulture).Length);
        }

        public DbfFieldDescriptor CreateDescriptor(string name) => _kind switch
        {
            Kind.Number when _integerWidth + (_scale == 0 ? 0 : 1 + _scale) > 20 =>
                throw new JsonException($"Numbers in property '{name}' exceed the DBF Numeric field's capacity."),
            Kind.Number => DbfFieldDescriptor.Numeric(name, 20, (byte)_scale),
            Kind.Logical => DbfFieldDescriptor.Logical(name),
            _ when _textLength > 254 || _useMemo => DbfFieldDescriptor.Memo(name),
            _ => DbfFieldDescriptor.Character(name, (byte)_textLength)
        };

        public DbfField Convert(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null) return default;
            return _kind switch
            {
                Kind.Number when _scale == 0 => new DbfField(checked((long)Number(value))),
                // DBase's untyped fractional Numeric formatter expects an exact Double value.
                Kind.Number => new DbfField((double)Number(value)),
                Kind.Logical => new DbfField(value.GetBoolean()),
                _ => new DbfField(Text(value))
            };
        }

        private static decimal Number(JsonElement value)
        {
            if (!value.TryGetDecimal(out var number) ||
                (decimal.Truncate(number) == number && (number < long.MinValue || number > long.MaxValue)))
                throw new JsonException("The JSON number cannot be represented by the inferred DBF Numeric field.");
            if (number == 0)
            {
                var mantissa = value.GetRawText().AsSpan();
                var exponent = mantissa.IndexOfAny('e', 'E');
                if (exponent >= 0) mantissa = mantissa[..exponent];
                if (mantissa.ContainsAnyInRange('1', '9'))
                    throw new JsonException("The JSON number is too small for the inferred DBF Numeric field.");
            }
            return number;
        }

        private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
            ? value.GetString()! : value.GetRawText();
    }
}

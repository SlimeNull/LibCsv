using LibCsv.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LibCsv
{
    public class CsvReader<TModel> : IDisposable
    {
        static readonly Type s_modelType = typeof(TModel);
        static readonly Dictionary<string, PropertyInfo> s_modelHeaderToProperty;

        static CsvReader()
        {
            s_modelType = typeof(TModel);
            s_modelHeaderToProperty = new Dictionary<string, PropertyInfo>();

            var properties = s_modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty);
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                if (property.GetCustomAttribute<HeaderAttribute>() is HeaderAttribute headerAttribute)
                {
                    s_modelHeaderToProperty[headerAttribute.Name ?? property.Name] = property;
                }
                else
                {
                    s_modelHeaderToProperty[property.Name] = property;
                }
            }
        }

        public CsvReader(TextReader reader)
        {
            BaseStream = null;

            _textReader = reader;
            _closeBaseStreamWhileDisposing = false;

            EnsureModelTypeOK();
        }

        public CsvReader(Stream stream)
        {
            BaseStream = stream;

            _textReader = new StreamReader(stream);
            _closeBaseStreamWhileDisposing = false;

            EnsureModelTypeOK();
        }

        public CsvReader(string file)
        {
            BaseStream = File.OpenRead(file);

            _textReader = new StreamReader(BaseStream);
            _closeBaseStreamWhileDisposing = true;

            EnsureModelTypeOK();
        }

        ~CsvReader()
        {
            Dispose(false);
        }

        private TModel? _current;
        private bool _closeBaseStreamWhileDisposing;
        private string[]? _headers;
        private TextReader _textReader;

        public Stream? BaseStream { get; }

        public TModel Current => _current ?? throw new InvalidOperationException("No data");

        private void EnsureModelTypeOK()
        {
            Type modelType = typeof(TModel);

            if (modelType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Array.Empty<Type>()) is null)
                throw new InvalidOperationException("Invalid TModel");

            //foreach (var prop in modelType.GetProperties())
            //{
            //    if (!prop.PropertyType.IsAssignableTo(typeof(IConvertible)))
            //        throw new InvalidOperationException($"Invalid TModel property: {prop.Name}, must be IConvertible");
            //}
        }

        private string[]? ReadRow()
        {
            string? line = _textReader.ReadLine();
            if (line == null)
                return null;

            return SplitCsvLine(line).ToArray();
        }

        public bool Read()
        {
            if (_headers == null)
            {
                string[]? header = ReadRow();
                if (header == null)
                    return false;

                this._headers = header;
            }

            string[]? row = ReadRow();
            if (row == null)
                return false;

            TModel model = Activator.CreateInstance<TModel>();
            MapRowToModel(_headers, row, model);

            _current = model;
            return true;
        }

        public void Close()
        {
            if (disposed)
                return;

            Dispose(true);
            disposed = true;
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        bool disposed = false;
        protected void Dispose(bool disposing)
        {
            if (disposing)
                GC.SuppressFinalize(this);

            if (_closeBaseStreamWhileDisposing)
                BaseStream?.Dispose();
        }

        private static IEnumerable<string> SplitCsvLine(string line)
        {
            StringBuilder stringBuilder = new StringBuilder();

            bool quote = false;
            bool escape = false;
            foreach (char c in line)
            {
                if (escape)
                {
                    stringBuilder.Append(
                        c switch
                        {
                            'r' => '\r',
                            't' => '\t',
                            'n' => '\n',
                            _ => c
                        });

                    escape = false;
                }
                else
                {
                    if (quote)
                    {
                        if (c == '"')
                            quote = false;
                        else if (c == '\\')
                        {
                            escape = true;
                        }
                        else
                        {
                            stringBuilder.Append(c);
                        }
                    }
                    else
                    {
                        if (c == ',')
                        {
                            yield return stringBuilder.ToString();
                            stringBuilder.Clear();
                        }
                        else if (c == '"')
                        {
                            quote = true;
                        }
                        else if (c == '\\')
                        {
                            escape = true;
                        }
                        else
                        {
                            stringBuilder.Append(c);
                        }
                    }
                }
            }

            if (stringBuilder.Length > 0)
                yield return stringBuilder.ToString();
        }

        private static string[] SplitWords(string name)
        {
            StringBuilder sb = new StringBuilder();
            List<string> words = new List<string>();

            bool upper = false;
            foreach (var c in name)
            {
                if (c == '_' || char.IsWhiteSpace(c))
                {
                    if (sb.Length != 0)
                    {
                        words.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else if (char.IsUpper(c) && !upper)
                {
                    if (sb.Length != 0)
                    {
                        words.Add(sb.ToString());
                        sb.Clear();
                    }

                    sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length != 0)
                words.Add(sb.ToString());

            return words.ToArray();
        }

        private static string ToPascal(string header)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var word in SplitWords(header))
            {
                sb.Append(char.ToUpper(word[0]));
                for (int i = 1; i < word.Length; i++)
                    sb.Append(word[i]);
            }

            return sb.ToString();
        }

        private static void MapRowToModel(string[] headers, string[] row, TModel model)
        {
            Type modelType = typeof(TModel);
            for (int i = 0; i < headers.Length; i++)
            {
                if (i >= row.Length)
                    break;

                string header = headers[i];
                if (!s_modelHeaderToProperty.TryGetValue(header, out var prop) &&
                    !s_modelHeaderToProperty.TryGetValue(ToPascal(header), out prop))
                {
                    continue;
                }

                if (prop == null)
                    continue;
                if (!prop.CanWrite)
                    continue;

                try
                {
                    if (prop.PropertyType != typeof(string) && string.IsNullOrEmpty(row[i]))
                    {
                        continue;
                    }


                    if (prop.PropertyType.IsEnum)
                    {
                        prop.SetValue(model, Enum.Parse(prop.PropertyType, row[i]));
                    }
                    else if(prop.PropertyType.IsAssignableTo(typeof(IConvertible)))
                    {
                        IConvertible convertible = row[i];
                        prop.SetValue(model, convertible.ToType(prop.PropertyType, null));
                    }
                    else 
                    {
                        prop.SetValue(model, JsonSerializer.Deserialize(row[i], prop.PropertyType));
                    }
                }
                catch (Exception ex)
                {
                    // ignore
                }
            }
        }
    }
}

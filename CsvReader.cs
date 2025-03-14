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
        static readonly Type ModelType = typeof(TModel);
        static readonly PropertyInfo[] ModelProperties = ModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty);

        public CsvReader(TextReader reader)
        {
            BaseStream = null;

            textReader = reader;
            closeBaseStreamWhileDisposing = false;

            EnsureModelTypeOK();
        }

        public CsvReader(Stream stream)
        {
            BaseStream = stream;

            textReader = new StreamReader(stream);
            closeBaseStreamWhileDisposing = false;

            EnsureModelTypeOK();
        }

        public CsvReader(string file)
        {
            BaseStream = File.OpenRead(file);

            textReader = new StreamReader(BaseStream);
            closeBaseStreamWhileDisposing = true;

            EnsureModelTypeOK();
        }

        ~CsvReader()
        {
            Dispose(false);
        }

        private TModel? current;
        private bool closeBaseStreamWhileDisposing;
        private string[]? header;
        private TextReader textReader;

        public Stream? BaseStream { get; }

        public TModel Current => current ?? throw new InvalidOperationException("No data");

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
            string? line = textReader.ReadLine();
            if (line == null)
                return null;

            return SplitCsvLine(line).ToArray();
        }

        public bool Read()
        {
            if (header == null)
            {
                string[]? header = ReadRow();
                if (header == null)
                    return false;

                this.header = header;
            }

            string[]? row = ReadRow();
            if (row == null)
                return false;

            TModel model = Activator.CreateInstance<TModel>();
            MapRowToModel(header, row, model);

            current = model;
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

            if (closeBaseStreamWhileDisposing)
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
                PropertyInfo? prop = modelType.GetProperty(header);

                if (prop == null)
                {
                    string pascalHeader = ToPascal(header);
                    prop = modelType.GetProperty(pascalHeader);
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

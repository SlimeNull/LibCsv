using LibCsv.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LibCsv
{
    public class CsvWriter<TModel> : IDisposable
    {
        static readonly Type s_modelType;
        static readonly PropertyInfo[] s_modelProperties;
        static readonly string[] s_modelHeaderNames;

        static CsvWriter()
        {
            s_modelType = typeof(TModel);
            s_modelProperties = s_modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty);

            s_modelHeaderNames = new string[s_modelProperties.Length];
            for (int i = 0; i < s_modelProperties.Length; i++)
            {
                var property = s_modelProperties[i];

                if (property.GetCustomAttribute<HeaderAttribute>() is HeaderAttribute headerAttribute)
                {
                    s_modelHeaderNames[i] = headerAttribute.Name ?? property.Name;
                }
                else
                {
                    s_modelHeaderNames[i] = property.Name;
                }
            }
        }

        public CsvWriter(TextWriter writer)
        {
            BaseStream = null;

            _textWriter = writer;
            _closeBaseStreamWhileDisposing = false;
        }

        public CsvWriter(Stream stream)
        {
            BaseStream = stream;

            _textWriter = new StreamWriter(stream);
            _closeBaseStreamWhileDisposing = false;
        }

        public CsvWriter(string filename)
        {
            BaseStream = File.Create(filename);

            _textWriter = new StreamWriter(BaseStream);
            _closeBaseStreamWhileDisposing = true;
        }

        ~CsvWriter()
        {
            Dispose(false);
        }

        private readonly TextWriter _textWriter;
        private readonly bool _closeBaseStreamWhileDisposing;
        private bool _headerWrote = false;

        public Stream? BaseStream { get; }




        private void WriteHeader()
        {
            string headerLine = string.Join(",", s_modelHeaderNames.Select(name => ToCsvCell(name)));
            _textWriter.WriteLine(headerLine);

            _headerWrote = true;
        }

        private string ToCsvCell(string? value)
        {
            if (value == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder(value.Length);

            bool hasComma = false;
            foreach (var c in value)
            {
                if (c == ',')
                    hasComma = true;

                sb.Append(c switch
                {
                    '\n' => @"\n",
                    '\r' => @"\r",
                    '\t' => @"\t",
                    '\f' => @"\f",
                    '\"' => "\"",
                    _ => c
                });
            }

            if (hasComma)
            {
                sb.Insert(0, '"');
                sb.Append('"');
            }

            return sb.ToString();
        }

        public void Write(TModel value)
        {
            if (!_headerWrote)
                WriteHeader();

            string dataLine = string.Join(",", s_modelProperties.Select(p => ToCsvCell(p.GetValue(value)?.ToString())));

            _textWriter.WriteLine(dataLine);
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
    }
}

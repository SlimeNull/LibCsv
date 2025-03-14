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
        static readonly Type ModelType = typeof(TModel);
        static readonly PropertyInfo[] ModelProperties = ModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty);

        public CsvWriter(TextWriter writer)
        {
            BaseStream = null;

            textWriter = writer;
            closeBaseStreamWhileDisposing = false;
        }

        public CsvWriter(Stream stream)
        {
            BaseStream = stream;

            textWriter = new StreamWriter(stream);
            closeBaseStreamWhileDisposing = false;
        }

        public CsvWriter(string filename)
        {
            BaseStream = File.Create(filename);

            textWriter = new StreamWriter(BaseStream);
            closeBaseStreamWhileDisposing = true;
        }

        ~CsvWriter()
        {
            Dispose(false);
        }

        private readonly TextWriter textWriter;
        private readonly bool closeBaseStreamWhileDisposing;
        private bool headerWrote = false;

        public Stream? BaseStream { get; }




        private void WriteHeader()
        {
            string headerLine = string.Join(",", ModelProperties.Select(p => p.Name));
            textWriter.WriteLine(headerLine);

            headerWrote = true;
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
            if (!headerWrote)
                WriteHeader();

            string dataLine = string.Join(",", ModelProperties.Select(p => ToCsvCell(p.GetValue(value)?.ToString())));

            textWriter.WriteLine(dataLine);
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
    }
}

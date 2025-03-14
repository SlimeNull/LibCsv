using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibCsv
{
    public class CsvSource<TModel>
    {
        public static IEnumerable<TModel> ReadAll(Stream stream)
        {
            using var reader = new CsvReader<TModel>(stream);
            while (reader.Read())
                yield return reader.Current;
        }

        public static IEnumerable<TModel> ReadAll(string filename)
        {
            using var reader = new CsvReader<TModel>(filename);
            while (reader.Read())
                yield return reader.Current;
        }
    }
}

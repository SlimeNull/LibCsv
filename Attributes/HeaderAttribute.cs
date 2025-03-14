using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibCsv.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class HeaderAttribute : Attribute
    {
        public string? Name { get; set; }

        public HeaderAttribute()
        { }

        public HeaderAttribute(string name)
        {
            Name = name;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntegrationTest
{
    public class Class1
    {
        public void M1(string value1, object value2)
        {
        }

        public void M2(object? value1, object? value2)
        {
            var text = value1?.ToString();

            if (text == null)
                return;

            M1(text, value1);

            var x = Enumerable.Range(0, 1)
                .Select(i => value2)
                .Where(item => item != null)
                .Select(item => item!.ToString())
                .FirstOrDefault()!;
        }
    }
}

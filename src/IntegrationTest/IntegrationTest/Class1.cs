using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrationTest
{
    public class Class1
    {
        public void M1(string value1, object value2)
        {

        }

        public void M2(object? value)
        {
            var text = value?.ToString();
            
            if (text == null)
                return;

            M1(text, value);
        }
    }
}

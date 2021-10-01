using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrationTest
{
    class ScreenShots
    {
        static void Test(object value1, object? value2)
        {
            var target1 = value1!.ToString();
            var target2 = value2!.ToString();

            if (target1 != null && target2 != null)
            {
                Console.WriteLine("Don't blame me for that sample code");
            }
        }
    }
}

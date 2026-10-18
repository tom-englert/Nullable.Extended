using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Sample
{
    public class ClassWithWarnings
    {
        private readonly IReadOnlyCollection<Mapping> _mappings;

        public ClassWithWarnings()
        {
            _mappings = Enumerable.Empty<Mapping>()
                .Where(item => item.Value != null)
                .OrderBy(item => item.Value.Length)
                .ToList()
                .AsReadOnly();
        }

        public void Test(JsonObject request)
        {
            foreach (var (key, value) in request)
            {
                var mapping = _mappings.FirstOrDefault(item => "something".StartsWith(item.Value, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public class ClassWithSuppressions
    {
        private readonly IReadOnlyCollection<Mapping> _mappings;

        public ClassWithSuppressions()
        {
            _mappings = Enumerable.Empty<Mapping>()
                .Where(item => item.Value != null)
                .OrderBy(item => item.Value!.Length)
                .ToList()
                .AsReadOnly();
        }

        public void Test(JsonObject request)
        {
            foreach (var (key, value) in request)
            {
                var mapping = _mappings.FirstOrDefault(item => "something".StartsWith(item.Value!, StringComparison.OrdinalIgnoreCase));
            }
        }

    }
    class Mapping
    {
        public string? Key;
        public string? Value { get; set; }
    }
}

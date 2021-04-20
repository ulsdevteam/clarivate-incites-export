using System.Security.AccessControl;
using CsvHelper.Configuration.Attributes;

namespace clarivate_incites_export
{
    public class ResearcherIdValuesRecord
    {
        public string Username { get; set; }
        [Name("Display Name")]
        public string DisplayName { get; set; }
        public string Name { get; set; }
        [Name("Identifier Value")]
        public string IdentifierValue { get; set; }
    }
}
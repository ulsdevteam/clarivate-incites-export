using CommandLine;

namespace clarivate_incites_export;

class Options
{
    [Value(0, Required = true, MetaName = "Org Hierarchy csv output path")]
    public string OrgHierarchyCsvOutputPath { get; set; }

    [Value(1, Required = true, MetaName = "Researchers csv output path")]
    public string ResearchersCsvOutputPath { get; set; }
}
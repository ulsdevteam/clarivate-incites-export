using CommandLine;

namespace clarivate_incites_export;

class Options
{
    [Option('o', "org", HelpText = "Org Hierarchy csv output path")]
    public string OrgHierarchyCsvOutputPath { get; set; }

    [Option('r', "res", HelpText = "Researchers csv output path")]
    public string ResearchersCsvOutputPath { get; set; }
}
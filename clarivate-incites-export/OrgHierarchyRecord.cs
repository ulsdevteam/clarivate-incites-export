using CsvHelper.Configuration.Attributes;

namespace clarivate_incites_export
{
    class OrgHierarchyRecord
    {
        [Index(0)]
        public string OrganizationId { get; set; }
        [Index(1)]
        public string OrganizationName { get; set; }
        [Index(2)]
        public string ParentOrgId { get; set; }
    }    
}
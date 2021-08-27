using System;
using System.Collections.Generic;
using System.Linq;

namespace clarivate_incites_export
{
    public class HierarchyBuilder 
    {
        private record LevelFunctions(Func<EmployeeData, string> GroupFn, Func<EmployeeData, OrgHierarchyRecord, string> IdFn, Func<EmployeeData, OrgHierarchyRecord, string> NameFn);

        private OrgHierarchyRecord TopLevelRecord { get; set; }
        private List<LevelFunctions> Levels { get; } = new List<LevelFunctions>();

        public HierarchyBuilder TopLevel(string orgId, string orgName) 
        {
            TopLevelRecord = new OrgHierarchyRecord {
                OrganizationID = orgId,
                OrganizationName = orgName
            };
            return this;
        }

        public HierarchyBuilder Then(Func<EmployeeData, string> groupFn, Func<EmployeeData, OrgHierarchyRecord, string> idFn, Func<EmployeeData, OrgHierarchyRecord, string> nameFn)
        {
            Levels.Add(new LevelFunctions(groupFn, idFn, nameFn));
            return this;
        }

        public List<OrgHierarchyRecord> Build(IEnumerable<EmployeeData> allEmployees)
        {
            var orgs = new List<OrgHierarchyRecord> { TopLevelRecord };
            RunLevel(0, TopLevelRecord, allEmployees);
            return orgs;

            void RunLevel(int level, OrgHierarchyRecord parentOrg, IEnumerable<EmployeeData> employees)
            {
                if (Levels.Count <= level) return;
                var (groupFn, idFn, nameFn) = Levels[level];
                foreach (var group in employees.GroupBy(groupFn))
                {
                    var representative = group.First();
                    var org = new OrgHierarchyRecord {
                        OrganizationID = idFn(representative, parentOrg),
                        OrganizationName = nameFn(representative, parentOrg),
                        ParentOrgaID = parentOrg.OrganizationID
                    };
                    orgs.Add(org);
                    RunLevel(level + 1, org, group);
                }
            }
        }
    }
}
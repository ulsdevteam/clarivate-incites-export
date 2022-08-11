using System;
using System.Collections.Generic;
using System.Linq;

namespace clarivate_incites_export
{
    public class HierarchyBuilder 
    {
        private record LevelFunctions(Func<EmployeeData, bool> CondFn, Func<EmployeeData, object> GroupFn, Func<EmployeeData, OrgHierarchyRecord, string> IdFn, Func<EmployeeData, OrgHierarchyRecord, string> NameFn);

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

        public HierarchyBuilder Then(Func<EmployeeData, object> groupFn, Func<EmployeeData, OrgHierarchyRecord, string> idFn, Func<EmployeeData, OrgHierarchyRecord, string> nameFn)
        {
            Levels.Add(new LevelFunctions(_ => true, groupFn, idFn, nameFn));
            return this;
        }

        public HierarchyBuilder ThenCond(Func<EmployeeData, bool> condFn, Func<EmployeeData, object> groupFn, Func<EmployeeData, OrgHierarchyRecord, string> idFn, Func<EmployeeData, OrgHierarchyRecord, string> nameFn)
        {
            Levels.Add(new LevelFunctions(condFn, groupFn, idFn, nameFn));
            return this;
        }

        public List<OrgHierarchyRecord> Build(IEnumerable<EmployeeData> allEmployees)
        {
            var orgs = new List<OrgHierarchyRecord> { TopLevelRecord };
            if (Levels.Any()) RunLevel(0, TopLevelRecord, allEmployees);
            return orgs;

            void RunLevel(int level, OrgHierarchyRecord parentOrg, IEnumerable<EmployeeData> employees)
            {
                if (level >= Levels.Count) { return; }
                var (condFn, groupFn, idFn, nameFn) = Levels[level];
                var (appliesEmployees, skipEmployees) = employees.SplitBy(condFn);
                foreach (var group in appliesEmployees.GroupBy(groupFn))
                {
                    var representative = group.First();
                    var org = new OrgHierarchyRecord
                    {
                        OrganizationID = idFn(representative, parentOrg),
                        OrganizationName = nameFn(representative, parentOrg),
                        ParentOrgaID = parentOrg.OrganizationID
                    };
                    orgs.Add(org);
                    // An employee is on a leaf if this level applies to them and no subsequent levels do
                    var (leaves, nonLeaves) =
                        group.SplitBy(e => !Levels.Skip(level).Any(l => l.CondFn(e)));
                    foreach (var employee in leaves)
                    {
                        employee.LeafOrganizationID = org.OrganizationID;
                        employee.LeafOrganizationName = org.OrganizationName;
                    }

                    RunLevel(level + 1, org, nonLeaves);
                }

                RunLevel(level + 1, parentOrg, skipEmployees);
            }
        }
    }
}
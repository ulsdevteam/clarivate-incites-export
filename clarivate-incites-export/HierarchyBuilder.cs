using System;
using System.Collections.Generic;
using System.Linq;

namespace clarivate_incites_export
{
    class HierarchyBuilder
    {
        OrgHierarchyRecord TopLevelRecord { get; set; }
        List<LevelFunctions> Levels { get; } = new();

        public HierarchyBuilder TopLevel(string orgId, string orgName)
        {
            TopLevelRecord = new OrgHierarchyRecord
            {
                OrganizationID = orgId,
                OrganizationName = orgName
            };
            return this;
        }

        public HierarchyBuilder Then(Func<EmployeeData, object> groupFn,
            Func<EmployeeData, OrgHierarchyRecord, string> idFn, Func<EmployeeData, OrgHierarchyRecord, string> nameFn)
        {
            Levels.Add(new LevelFunctions(_ => true, groupFn, idFn, nameFn));
            return this;
        }

        public HierarchyBuilder ThenCond(Func<EmployeeData, bool> condFn, Func<EmployeeData, object> groupFn,
            Func<EmployeeData, OrgHierarchyRecord, string> idFn, Func<EmployeeData, OrgHierarchyRecord, string> nameFn)
        {
            Levels.Add(new LevelFunctions(condFn, groupFn, idFn, nameFn));
            return this;
        }

        public (List<OrgHierarchyRecord>, List<ResearcherRecord>) BuildRecords(
            IEnumerable<EmployeeData> allEmployees, IdentifierLookup idLookup)
        {
            var orgs = new List<OrgHierarchyRecord> { TopLevelRecord };
            var researchers = new List<ResearcherRecord>();
            if (Levels.Any()) { RunLevel(0, TopLevelRecord, allEmployees); }

            return (orgs, researchers);

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
                        group.SplitBy(e => !Levels.Skip(level + 1).Any(l => l.CondFn(e)));
                    researchers.AddRange(leaves.Select(employee =>
                        new ResearcherRecord(employee, org,
                            idLookup.GetIdentifiers(employee.EMPLID, employee.USERNAME))));

                    RunLevel(level + 1, org, nonLeaves);
                }

                RunLevel(level + 1, parentOrg, skipEmployees);
            }
        }

        record LevelFunctions(Func<EmployeeData, bool> CondFn, Func<EmployeeData, object> GroupFn,
            Func<EmployeeData, OrgHierarchyRecord, string> IdFn, Func<EmployeeData, OrgHierarchyRecord, string> NameFn);
    }
}
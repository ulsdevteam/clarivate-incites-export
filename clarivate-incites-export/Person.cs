using System;
using System.Collections.Generic;

namespace clarivate_incites_export
{
    public class Person
    {
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string OrganizationId { get; set; }
        public HashSet<Identifier> Identifiers { get; set; } = new HashSet<Identifier>();

        public void Combine(Person other)
        {
            if (!string.Equals(other.Username, Username, StringComparison.InvariantCultureIgnoreCase))
                throw new InvalidOperationException("Cannot combine two different people.");
            FirstName ??= other.FirstName;
            LastName ??= other.LastName;
            OrganizationId ??= other.OrganizationId;
            Identifiers.UnionWith(other.Identifiers);
        }
    }
}
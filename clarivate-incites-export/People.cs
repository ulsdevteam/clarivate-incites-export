using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace clarivate_incites_export
{
    public class People : KeyedCollection<string, Person>
    {
        protected override string GetKeyForItem(Person person) => person.Username?.ToUpperInvariant() ?? person.GetHashCode().ToString();

        public static People From(params IEnumerable<Person>[] sources)
        {
            var people = new People();
            foreach (var source in sources)
                foreach (var person in source)
                    people.AddOrUpdate(person);
            return people;
        }

        public void AddOrUpdate(Person person)
        {
            if (TryGetValue(GetKeyForItem(person), out var existingPerson))
                existingPerson.Combine(person);
            else
                Add(person);
        }
    }
}
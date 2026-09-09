using System;

namespace IrisMenus
{
    public sealed class MenuSearchEntry
    {
        public string Id { get; }
        public Func<string> Title { get; }
        public Func<string> Keywords { get; }
        public Func<string> Context { get; }

        public MenuSearchEntry(string id, Func<string> title,
            Func<string> keywords = null, Func<string> context = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A search entry ID is required.", nameof(id));
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Keywords = keywords;
            Context = context;
        }
    }
}

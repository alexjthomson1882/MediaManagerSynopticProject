using System;
using System.Collections.Generic;

namespace MediaManager {

    public sealed class Category {

        #region variable

        public readonly string name;

        private static readonly Dictionary<string, Category> CategoryCache = new Dictionary<string, Category>();

        #endregion

        #region constructor

        private Category(in string name) {

            this.name = name ?? throw new ArgumentNullException("name");

        }

        #endregion

        #region logic

        public static Category GetCategory(in string name) {

            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");

            string lowerName = name.ToLower();

            Category category;
            if (CategoryCache.TryGetValue(lowerName, out category)) return category;
            category = new Category(name);
            CategoryCache.Add(lowerName, category);
            return category;

        }

        #endregion

    }

}

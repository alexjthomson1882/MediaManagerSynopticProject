using System;
using System.IO;
using System.Text;

namespace MediaManager {

    public sealed class MediaFile : IIdentifiable {

        #region variable

        public readonly string name;

        public readonly MediaFileType type;

        public readonly Uri location;

        public readonly FileInfo fileInfo;

        public readonly MediaDirectory parent;

        public string comment;

        public string image;

        public Category[] categories;

        #endregion

        #region property

        #endregion

        #region constructor

        public MediaFile(in string location, in MediaDirectory parent) {
            
            this.location = new Uri(location ?? throw new ArgumentNullException("location"), UriKind.Absolute);
            this.parent = parent ?? throw new ArgumentNullException("parent");

            name = Path.GetFileNameWithoutExtension(location);
            fileInfo = new FileInfo(location);

            type = MediaFileTypeUtility.FromExtension(fileInfo.Extension);
            //if (type == MediaFileType.Unknown) throw new NotSupportedException("Unsupported file type: " + fileInfo.Extension);
            categories = new Category[0];

        }

        #endregion

        #region logic

        public string GetGUID() => $"{parent.GetGUID()}:{location.AbsolutePath.GetGUID()}";

        public string CategoriesToString() {

            if (categories.Length == 0) return string.Empty;
            StringBuilder stringBuilder = new StringBuilder(categories.Length * 8);
            stringBuilder.Append(categories[0].name);
            for (int i = 1; i < categories.Length; i++) {

                stringBuilder.Append(", ");
                stringBuilder.Append(categories[i].name);

            }
            return stringBuilder.ToString();

        }

        public void Dispose() {



        }

        #endregion

    }

}
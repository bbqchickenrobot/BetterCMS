﻿using System;
using System.Collections.Generic;

using BetterCms.Core.Models;
using BetterCms.Module.MediaManager.Models;
using BetterCms.Module.Root.Models;

namespace BetterCms.Module.Pages.Models
{
    [Serializable]
    public class PageProperties : Page
    {
        public virtual string Description { get; set; }
        public virtual string CanonicalUrl { get; set; }
        public virtual string CustomCss { get; set; }
        public virtual string CustomJS { get; set; }

        public virtual bool UseCanonicalUrl { get; set; }
        public virtual bool UseNoFollow { get; set; }
        public virtual bool UseNoIndex { get; set; }

        public virtual int NodeCountInSitemap { get; set; }

        public override bool HasSEO
        {
            get
            {
                return base.HasSEO && NodeCountInSitemap > 0;
            }
        }

        public virtual IList<PageTag> PageTags { get; set; }
        
        public virtual Category Category { get; set; }
        public virtual MediaImage Image { get; set; }

        public virtual PageProperties Duplicate()
        {
            return CopyDataToDuplicate(new PageProperties());
        }

        protected virtual PageProperties CopyDataToDuplicate(PageProperties duplicate)
        {
            duplicate.MetaTitle = MetaTitle;
            duplicate.MetaKeywords = MetaKeywords;
            duplicate.MetaDescription = MetaDescription;
            duplicate.UseCanonicalUrl = UseCanonicalUrl;
            duplicate.CustomCss = CustomCss;
            duplicate.CustomJS = CustomJS;
            duplicate.Description = Description;
            duplicate.UseNoFollow = UseNoFollow;
            duplicate.UseNoIndex = UseNoIndex;
            duplicate.Layout = Layout;
            duplicate.Image = Image;
            duplicate.Category = Category;

            return duplicate;
        }
    }
}
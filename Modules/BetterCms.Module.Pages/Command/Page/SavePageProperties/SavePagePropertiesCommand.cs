﻿using System;
using System.Collections.Generic;
using System.Linq;

using BetterCms.Core.DataAccess.DataContext;
using BetterCms.Core.DataAccess.DataContext.Fetching;
using BetterCms.Core.DataContracts.Enums;
using BetterCms.Core.Exceptions;
using BetterCms.Core.Exceptions.Mvc;
using BetterCms.Core.Mvc.Commands;
using BetterCms.Core.Security;

using BetterCms.Module.MediaManager.Models;

using BetterCms.Module.Pages.Content.Resources;
using BetterCms.Module.Pages.Models;
using BetterCms.Module.Pages.Models.Events;
using BetterCms.Module.Pages.Services;
using BetterCms.Module.Pages.ViewModels.Page;

using BetterCms.Module.Root;
using BetterCms.Module.Root.Models;
using BetterCms.Module.Root.Models.Extensions;
using BetterCms.Module.Root.Mvc;
using BetterCms.Module.Root.Mvc.Helpers;
using BetterCms.Module.Root.Services;

using CategoryEntity = BetterCms.Module.Root.Models.Category;

namespace BetterCms.Module.Pages.Command.Page.SavePageProperties
{
    /// <summary>
    /// Page properties save command.
    /// </summary>
    public class SavePagePropertiesCommand : CommandBase, ICommand<EditPagePropertiesViewModel, SavePageResponse>
    {
        /// <summary>
        /// The page service
        /// </summary>
        private readonly IPageService pageService;

        /// <summary>
        /// The redirect service
        /// </summary>
        private readonly IRedirectService redirectService;

        /// <summary>
        /// The tag service
        /// </summary>
        private readonly ITagService tagService;

        /// <summary>
        /// The sitemap service.
        /// </summary>
        private readonly ISitemapService sitemapService;

        /// <summary>
        /// The url service
        /// </summary>
        private readonly IUrlService urlService;

        /// <summary>
        /// The options service
        /// </summary>
        private readonly IOptionService optionService;

        /// <summary>
        /// The CMS configuration
        /// </summary>
        private readonly ICmsConfiguration cmsConfiguration;

        /// <summary>
        /// The access control service
        /// </summary>
        private readonly IAccessControlService accessControlService;

        /// <summary>
        /// The content service
        /// </summary>
        private readonly IContentService contentService;

        /// <summary>
        /// The master page service
        /// </summary>
        private readonly IMasterPageService masterPageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SavePagePropertiesCommand" /> class.
        /// </summary>
        /// <param name="pageService">The page service.</param>
        /// <param name="redirectService">The redirect service.</param>
        /// <param name="tagService">The tag service.</param>
        /// <param name="sitemapService">The sitemap service.</param>
        /// <param name="urlService">The URL service.</param>
        /// <param name="optionService">The option service.</param>
        /// <param name="cmsConfiguration">The CMS configuration.</param>
        /// <param name="accessControlService">The access control service.</param>
        /// <param name="contentService">The content service.</param>
        /// <param name="masterPageService">The master page service.</param>
        public SavePagePropertiesCommand(IPageService pageService, IRedirectService redirectService, ITagService tagService,
            ISitemapService sitemapService, IUrlService urlService, IOptionService optionService,
            ICmsConfiguration cmsConfiguration, IAccessControlService accessControlService, IContentService contentService,
            IMasterPageService masterPageService)
        {
            this.pageService = pageService;
            this.redirectService = redirectService;
            this.tagService = tagService;
            this.sitemapService = sitemapService;
            this.urlService = urlService;
            this.optionService = optionService;
            this.cmsConfiguration = cmsConfiguration;
            this.accessControlService = accessControlService;
            this.contentService = contentService;
            this.masterPageService = masterPageService;
        }

        /// <summary>
        /// Executes the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Save response.</returns>
        /// <exception cref="CmsException">Failed to save page properties.</exception>
        public SavePageResponse Execute(EditPagePropertiesViewModel request)
        {
            if (!request.MasterPageId.HasValue && !request.TemplateId.HasValue)
            {
                var logMessage = string.Format("Template or master page should be selected for page {0}.", request.Id);
                throw new ValidationException(() => PagesGlobalization.SavePagePropertiesCommand_NoLayoutOrMasterSelected_Message, logMessage);
            }

            if (request.MasterPageId.HasValue && request.TemplateId.HasValue)
            {
                var logMessage = string.Format("Only one of master page and layout can be selected for page {0}.", request.Id);
                throw new ValidationException(() => PagesGlobalization.SavePagePropertiesCommand_LayoutAndMasterIsSelected_Message, logMessage);
            }

            if (request.MasterPageId.HasValue)
            {
                if (request.Id == request.MasterPageId.Value)
                {
                    var logMessage = string.Format("Selected master page is the current page {0}.", request.Id);
                    throw new ValidationException(() => PagesGlobalization.SavePagePropertiesCommand_SelectedMasterIsCurrentPage_Message, logMessage);
                }

                if (Repository.AsQueryable<MasterPage>().Where(m => m.Page.Id == request.MasterPageId.Value).Any(m => m.Master.Id == request.Id))
                {
                    var logMessage = string.Format("Selected master page {0} is a child of the current page {1}.", request.MasterPageId.Value, request.Id);
                    throw new ValidationException(() => PagesGlobalization.SavePagePropertiesCommand_SelectedMasterIsChildPage_Message, logMessage);
                }
            }
            
            var pageQuery = Repository
                .AsQueryable<PageProperties>(p => p.Id == request.Id)
                .FetchMany(p => p.Options)
                .Fetch(p => p.Layout).ThenFetchMany(l => l.LayoutOptions)
                .FetchMany(p => p.MasterPages)
                .AsQueryable();
            
            if (cmsConfiguration.Security.AccessControlEnabled)
            {
                pageQuery = pageQuery.FetchMany(f => f.AccessRules);
            }

            var page = pageQuery.ToList().FirstOne();
            var beforeChange = new UpdatingPagePropertiesModel(page);

            var roles = page.IsMasterPage
                            ? new[] { RootModuleConstants.UserRoles.EditContent, RootModuleConstants.UserRoles.PublishContent, RootModuleConstants.UserRoles.Administration }
                            : new[] { RootModuleConstants.UserRoles.EditContent, RootModuleConstants.UserRoles.PublishContent };

            if (cmsConfiguration.Security.AccessControlEnabled)
            {
                AccessControlService.DemandAccess(page, Context.Principal, AccessLevel.ReadWrite, roles);
            }
            else
            {
                AccessControlService.DemandAccess(Context.Principal, roles);
            }

            // Load master pages for updating page's master path and page's children master path
            IList<Guid> newMasterIds;
            IList<Guid> oldMasterIds;
            IList<Guid> childrenPageIds;
            IList<MasterPage> existingChildrenMasterPages;
            if ((page.MasterPage != null && page.MasterPage.Id != request.MasterPageId) || (page.MasterPage == null && request.MasterPageId.HasValue))
            {
                newMasterIds = request.MasterPageId.HasValue 
                    ? masterPageService.GetPageMasterPageIds(request.MasterPageId.Value) 
                    : new List<Guid>(0);

                oldMasterIds = page.MasterPage != null && page.MasterPages != null 
                    ? page.MasterPages.Select(mp => mp.Master.Id).Distinct().ToList() 
                    : new List<Guid>(0);

                var intersectingIds = newMasterIds.Intersect(oldMasterIds).ToArray();
                foreach (var id in intersectingIds)
                {
                    oldMasterIds.Remove(id);
                    newMasterIds.Remove(id);
                }

                var updatingIds = newMasterIds.Union(oldMasterIds).Distinct().ToList();
                existingChildrenMasterPages = GetChildrenMasterPagesToUpdate(page, updatingIds, out childrenPageIds);
            }
            else
            {
                newMasterIds = null;
                oldMasterIds = null;
                childrenPageIds = null;
                existingChildrenMasterPages = null;
            }

            var canEdit = page.IsMasterPage
                ? SecurityService.IsAuthorized(Context.Principal, RootModuleConstants.UserRoles.MultipleRoles(RootModuleConstants.UserRoles.EditContent, RootModuleConstants.UserRoles.Administration))
                : SecurityService.IsAuthorized(Context.Principal, RootModuleConstants.UserRoles.EditContent);

            IList<SitemapNode> updatedNodes = null;

            // Start transaction, only when everything is already loaded
            UnitOfWork.BeginTransaction();

            Models.Redirect redirectCreated = null;
            var initialSeoStatus = page.HasSEO;

            request.PageUrl = urlService.FixUrl(request.PageUrl);

            if (canEdit && !string.Equals(page.PageUrl, request.PageUrl))
            {
                pageService.ValidatePageUrl(request.PageUrl, request.Id);
                if (request.RedirectFromOldUrl)
                {
                    var redirect = redirectService.CreateRedirectEntity(page.PageUrl, request.PageUrl);
                    if (redirect != null)
                    {
                        Repository.Save(redirect);
                        redirectCreated = redirect;
                    }
                }

                if (request.UpdateSitemap)
                {
                    updatedNodes = sitemapService.ChangeUrlsInAllSitemapsNodes(page.PageUrl, request.PageUrl);
                }

                page.PageUrl = request.PageUrl;
            }

            if (canEdit)
            {
                page.PageUrlHash = page.PageUrl.UrlHash();
                page.Category = request.CategoryId.HasValue ? Repository.AsProxy<CategoryEntity>(request.CategoryId.Value) : null;
                page.Title = request.PageName;
                page.CustomCss = request.PageCSS;
                page.CustomJS = request.PageJavascript;

                if (request.MasterPageId.HasValue)
                {
                    if (page.MasterPage == null || page.MasterPage.Id != request.MasterPageId.Value)
                    {
                        page.MasterPage = Repository.AsProxy<Root.Models.Page>(request.MasterPageId.Value);
                    }
                    page.Layout = null;
                }
                else
                {
                    if (page.Layout == null || page.Layout.Id != request.TemplateId.Value)
                    {
                        page.Layout = Repository.First<Root.Models.Layout>(request.TemplateId.Value);
                    }
                    page.MasterPage = null;
                }
            }

            var publishDraftContent = false;
            if (request.CanPublishPage && !page.IsMasterPage)
            {
                AccessControlService.DemandAccess(Context.Principal, RootModuleConstants.UserRoles.PublishContent);

                if (request.IsPagePublished)
                {
                    if (page.Status != PageStatus.Published)
                    {
                        page.Status = PageStatus.Published;
                        page.PublishedOn = DateTime.Now;
                        publishDraftContent = true;
                    }
                }
                else
                {
                    page.Status = PageStatus.Unpublished;
                }
            }

            if (canEdit)
            {
                if (!page.IsMasterPage)
                {
                    page.UseNoFollow = request.UseNoFollow;
                    page.UseNoIndex = request.UseNoIndex;
                    page.IsArchived = request.IsArchived;
                }

                page.UseCanonicalUrl = request.UseCanonicalUrl;
                page.Version = request.Version;

                page.Image = request.Image != null && request.Image.ImageId.HasValue ? Repository.AsProxy<MediaImage>(request.Image.ImageId.Value) : null;
                page.SecondaryImage = request.SecondaryImage != null && request.SecondaryImage.ImageId.HasValue
                                          ? Repository.AsProxy<MediaImage>(request.SecondaryImage.ImageId.Value)
                                          : null;
                page.FeaturedImage = request.FeaturedImage != null && request.FeaturedImage.ImageId.HasValue
                                         ? Repository.AsProxy<MediaImage>(request.FeaturedImage.ImageId.Value)
                                         : null;

                var optionValues = page.Options.Distinct();
                optionService.SaveOptionValues(request.OptionValues, optionValues, () => new PageOption { Page = page });

                if (cmsConfiguration.Security.AccessControlEnabled)
                {
                    page.AccessRules.RemoveDuplicateEntities();

                    var accessRules = request.UserAccessList != null ? request.UserAccessList.Cast<IAccessRule>().ToList() : null;
                    accessControlService.UpdateAccessControl(page, accessRules);
                }
            }

            // Notify about page properties changing.
            var cancelEventArgs = Events.PageEvents.Instance.OnPagePropertiesChanging(beforeChange, new UpdatingPagePropertiesModel(page));
            if (cancelEventArgs.Cancel)
            {
                Context.Messages.AddError(cancelEventArgs.CancellationErrorMessages.ToArray());
                return null;
            }

            Repository.Save(page);

            IList<Tag> newTags = null;
            if (canEdit)
            {
                UpdateChildrenMasterPages(existingChildrenMasterPages, oldMasterIds, newMasterIds, childrenPageIds);
                tagService.SavePageTags(page, request.Tags, out newTags);
            }

            if (publishDraftContent)
            {
                contentService.PublishDraftContent(page.Id);
            }

            UnitOfWork.Commit();

            // Notify about page properties change.
            Events.PageEvents.Instance.OnPagePropertiesChanged(page);

            // Notify about redirect creation.
            if (redirectCreated != null)
            {
                Events.PageEvents.Instance.OnRedirectCreated(redirectCreated);
            }

            // Notify about SEO status change.
            if (initialSeoStatus != page.HasSEO)
            {                
                Events.PageEvents.Instance.OnPageSeoStatusChanged(page);
            }

            // Notify about new tags.
            Events.RootEvents.Instance.OnTagCreated(newTags);

            // Notify about updated sitemap nodes.
            if (updatedNodes != null)
            {
                var updatedSitemaps = new List<Models.Sitemap>();
                foreach (var node in updatedNodes)
                {
                    Events.SitemapEvents.Instance.OnSitemapNodeUpdated(node);
                    if (!updatedSitemaps.Contains(node.Sitemap))
                    {
                        updatedSitemaps.Add(node.Sitemap);
                    }
                }

                foreach (var updatedSitemap in updatedSitemaps)
                {
                    Events.SitemapEvents.Instance.OnSitemapUpdated(updatedSitemap);
                }
            }

            return new SavePageResponse(page);
        }

        /// <summary>
        /// Retrieves all the master page children, when master page is changed.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="updatingIds">The updating ids.</param>
        /// <param name="childrenPageIds">The children page ids.</param>
        /// <returns>
        /// List of all the childer master pages, which must be changed
        /// </returns>
        private List<MasterPage> GetChildrenMasterPagesToUpdate(PageProperties page, IList<Guid> updatingIds,
            out IList<Guid> childrenPageIds)
        {
            // Retrieve all master pages, refering old master and master, which include updating page also as master page
            var query = Repository
                .AsQueryable<MasterPage>()
                .Where(mp => mp.Page.MasterPages.Any(mp1 => mp1.Master == page) || mp.Page == page);

            childrenPageIds = query.Select(mp => mp.Page.Id).Distinct().ToList();
            if (!childrenPageIds.Contains(page.Id))
            {
                childrenPageIds.Add(page.Id);
            }
            
            return query.Where(mp => updatingIds.Contains(mp.Master.Id)).ToList();
        }

        /// <summary>
        /// Updates the master page children: instead of old master page inserts the new one.
        /// </summary>
        /// <param name="existingChildrenMasterPages">Already saved children master page assignments.</param>
        /// <param name="oldMasterIds">The old master ids.</param>
        /// <param name="newMasterIds">The new master ids.</param>
        /// <param name="childrenPageIds">The children page ids.</param>
        private void UpdateChildrenMasterPages(IList<MasterPage> existingChildrenMasterPages,
            IList<Guid> oldMasterIds, IList<Guid> newMasterIds, IEnumerable<Guid> childrenPageIds)
        {
            if (childrenPageIds == null)
            {
                return;
            }

            // Loop in all the distinct master pages
            foreach (var pageId in childrenPageIds)
            {
                // Delete master pages from path
                existingChildrenMasterPages
                    .Where(mp => mp.Page.Id == pageId && oldMasterIds.Contains(mp.Master.Id)).ToList()
                    .ForEach(mp => Repository.Delete(mp));

                // Add new ones
                newMasterIds
                    .Where(masterPageId => !existingChildrenMasterPages.Any(mp => mp.Page.Id == pageId && mp.Master.Id == masterPageId)).ToList()
                    .ForEach(masterPageId =>
                        {
                            var mp = new MasterPage
                                {
                                    Master = Repository.AsProxy<Root.Models.Page>(masterPageId),
                                    Page = Repository.AsProxy<Root.Models.Page>(pageId)
                                };
                            Repository.Save(mp);
                        });
            }
        }
    }
}
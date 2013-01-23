﻿using BetterCms.Core.Modules;
using BetterCms.Core.Modules.JsModule;
using BetterCms.Core.Modules.Projections;
using BetterCms.Module.Users.Content.Resources;
using BetterCms.Module.Users.Controllers;

namespace BetterCms.Module.Blog.Registration
{
    /// <summary>
    /// bcms.blog.js module descriptor.
    /// </summary>
    public class UserJavaScriptModuleDescriptor : JavaScriptModuleDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserJavaScriptModuleDescriptor" /> class.
        /// </summary>
        /// <param name="containerModule">The container module.</param>
        public UserJavaScriptModuleDescriptor(ModuleDescriptor containerModule)
            : base(containerModule, "bcms.user", "/file/bcms-users/scripts/bcms.user")
        {
            Links = new IActionProjection[]
                        {
                            new JavaScriptModuleLinkTo<UserController>(this, "loadSiteSettingsUsersUrl", c => c.Index(null)),
                            new JavaScriptModuleLinkTo<UserController>(this, "loadEditUserUrl", c=> c.EditUser()), 
                            new JavaScriptModuleLinkTo<RoleController>(this, "loadCreatRoleUrl", c => c.CreatRoleView()),
                            new JavaScriptModuleLinkTo<RoleController>(this, "loadEditRoleUrl", c => c.EditRoleView("{0}")),
                            new JavaScriptModuleLinkTo<RoleController>(this, "loadSiteSettingsRoleUrl", c => c.RolesListView(null)) 
                        };

            Globalization = new IActionProjection[]
                        {
                            new JavaScriptModuleGlobalization(this, "usersListTabTitle", () => UsersGlobalization.SiteSettings_Users_ListTab_Title), 
                            new JavaScriptModuleGlobalization(this, "usersAddNewTitle", () => UsersGlobalization.EditUser_Window_Title),
                            new JavaScriptModuleGlobalization(this, "rolesListTabTitle", () => UsersGlobalization.SiteSettings_Roles_ListTab_Title),
                            new JavaScriptModuleGlobalization(this, "rolesAddNewTitle", () => UsersGlobalization.Role_AddNeww_Dialog_Title) 
                        };
        }
    }
}
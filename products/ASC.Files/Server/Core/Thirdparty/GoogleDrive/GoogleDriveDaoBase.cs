/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using ASC.Common.Web;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Core.Tenants;
using ASC.FederatedLogin.LoginProviders;
using ASC.Files.Core;
using ASC.Files.Core.EF;
using ASC.Files.Core.Security;
using ASC.Security.Cryptography;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Studio.Core;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace ASC.Files.Thirdparty.GoogleDrive
{
    internal abstract class GoogleDriveDaoBase
    {
        public GoogleDriveDaoSelector GoogleDriveDaoSelector { get; set; }

        public int TenantID { get; set; }
        public GoogleDriveProviderInfo GoogleDriveProviderInfo { get; set; }
        public string PathPrefix { get; set; }
        public IServiceProvider ServiceProvider { get; }
        public UserManager UserManager { get; }
        public TenantUtil TenantUtil { get; }
        public FilesDbContext FilesDbContext { get; }
        public SetupInfo SetupInfo { get; }
        public FileUtility FileUtility { get; }

        public GoogleDriveDaoBase(
            IServiceProvider serviceProvider,
            UserManager userManager,
            TenantManager tenantManager,
            TenantUtil tenantUtil,
            DbContextManager<FilesDbContext> dbContextManager,
            SetupInfo setupInfo,
            FileUtility fileUtility)
        {
            ServiceProvider = serviceProvider;
            UserManager = userManager;
            TenantUtil = tenantUtil;
            TenantID = tenantManager.GetCurrentTenant().TenantId;
            FilesDbContext = dbContextManager.Get(FileConstant.DatabaseId);
            SetupInfo = setupInfo;
            FileUtility = fileUtility;
        }

        public void Init(GoogleDriveDaoSelector.GoogleDriveInfo googleDriveInfo, GoogleDriveDaoSelector googleDriveDaoSelector)
        {
            GoogleDriveProviderInfo = googleDriveInfo.GoogleDriveProviderInfo;
            PathPrefix = googleDriveInfo.PathPrefix;
            GoogleDriveDaoSelector = googleDriveDaoSelector;
        }

        public void Dispose()
        {
            GoogleDriveProviderInfo.Dispose();
        }

        protected string MappingID(string id, bool saveIfNotExist = false)
        {
            if (id == null) return null;

            string result;
            if (id.ToString().StartsWith("drive"))
            {
                result = Regex.Replace(BitConverter.ToString(Hasher.Hash(id.ToString(), HashAlg.MD5)), "-", "").ToLower();
            }
            else
            {
                result = FilesDbContext.ThirdpartyIdMapping
                        .Where(r => r.HashId == id)
                        .Select(r => r.Id)
                        .FirstOrDefault();
            }
            if (saveIfNotExist)
            {
                var newMapping = new DbFilesThirdpartyIdMapping
                {
                    Id = id,
                    HashId = result,
                    TenantId = TenantID
                };

                FilesDbContext.ThirdpartyIdMapping.Add(newMapping);
                FilesDbContext.SaveChanges();
            }

            return result;
        }

        protected IQueryable<T> Query<T>(DbSet<T> set) where T : class, IDbFile
        {
            return set.Where(r => r.TenantId == TenantID);
        }


        protected static string MakeDriveId(object entryId)
        {
            var id = Convert.ToString(entryId, CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(id)
                       ? "root"
                       : id.TrimStart('/');
        }

        protected static string GetParentDriveId(DriveFile driveEntry)
        {
            return driveEntry == null || driveEntry.Parents == null || driveEntry.Parents.Count == 0
                       ? null
                       : driveEntry.Parents[0];
        }

        protected string MakeId(DriveFile driveEntry)
        {
            var path = string.Empty;
            if (driveEntry != null)
            {
                path = IsRoot(driveEntry) ? "root" : driveEntry.Id;
            }

            return MakeId(path);
        }

        protected string MakeId(string path = null)
        {
            return string.Format("{0}{1}", PathPrefix,
                                 string.IsNullOrEmpty(path) || path == "root" || path == GoogleDriveProviderInfo.DriveRootId
                                     ? "" : ("-|" + path.TrimStart('/')));
        }

        protected string MakeFolderTitle(DriveFile driveFolder)
        {
            if (driveFolder == null || IsRoot(driveFolder))
            {
                return GoogleDriveProviderInfo.CustomerTitle;
            }

            return Global.ReplaceInvalidCharsAndTruncate(driveFolder.Name);
        }

        protected string MakeFileTitle(DriveFile driveFile)
        {
            if (driveFile == null || string.IsNullOrEmpty(driveFile.Name))
            {
                return GoogleDriveProviderInfo.ProviderKey;
            }

            var title = driveFile.Name;

            var gExt = MimeMapping.GetExtention(driveFile.MimeType);
            if (GoogleLoginProvider.GoogleDriveExt.Contains(gExt))
            {
                var downloadableExtension = FileUtility.GetGoogleDownloadableExtension(gExt);
                if (!downloadableExtension.Equals(FileUtility.GetFileExtension(title)))
                {
                    title += downloadableExtension;
                }
            }

            return Global.ReplaceInvalidCharsAndTruncate(title);
        }

        protected Folder<string> ToFolder(DriveFile driveEntry)
        {
            if (driveEntry == null) return null;
            if (driveEntry is ErrorDriveEntry)
            {
                //Return error entry
                return ToErrorFolder(driveEntry as ErrorDriveEntry);
            }

            if (driveEntry.MimeType != GoogleLoginProvider.GoogleDriveMimeTypeFolder)
            {
                return null;
            }

            var isRoot = IsRoot(driveEntry);

            var folder = ServiceProvider.GetService<Folder<string>>();

            folder.ID = MakeId(driveEntry);
            folder.ParentFolderID = isRoot ? null : MakeId(GetParentDriveId(driveEntry));
            folder.CreateBy = GoogleDriveProviderInfo.Owner;
            folder.CreateOn = isRoot ? GoogleDriveProviderInfo.CreateOn : (driveEntry.CreatedTime ?? default);
            folder.FolderType = FolderType.DEFAULT;
            folder.ModifiedBy = GoogleDriveProviderInfo.Owner;
            folder.ModifiedOn = isRoot ? GoogleDriveProviderInfo.CreateOn : (driveEntry.ModifiedTime ?? default);
            folder.ProviderId = GoogleDriveProviderInfo.ID;
            folder.ProviderKey = GoogleDriveProviderInfo.ProviderKey;
            folder.RootFolderCreator = GoogleDriveProviderInfo.Owner;
            folder.RootFolderId = MakeId();
            folder.RootFolderType = GoogleDriveProviderInfo.RootFolderType;

            folder.Shareable = false;
            folder.Title = MakeFolderTitle(driveEntry);
            folder.TotalFiles = 0;
            folder.TotalSubFolders = 0;

            if (folder.CreateOn != DateTime.MinValue && folder.CreateOn.Kind == DateTimeKind.Utc)
                folder.CreateOn = TenantUtil.DateTimeFromUtc(folder.CreateOn);

            if (folder.ModifiedOn != DateTime.MinValue && folder.ModifiedOn.Kind == DateTimeKind.Utc)
                folder.ModifiedOn = TenantUtil.DateTimeFromUtc(folder.ModifiedOn);

            return folder;
        }

        protected static bool IsRoot(DriveFile driveFolder)
        {
            return IsDriveFolder(driveFolder) && GetParentDriveId(driveFolder) == null;
        }

        private static bool IsDriveFolder(DriveFile driveFolder)
        {
            return driveFolder != null && driveFolder.MimeType == GoogleLoginProvider.GoogleDriveMimeTypeFolder;
        }

        private File<string> ToErrorFile(ErrorDriveEntry driveEntry)
        {
            if (driveEntry == null) return null;

            var file = ServiceProvider.GetService<File<string>>();
            file.ID = MakeId(driveEntry.ErrorId);
            file.CreateBy = GoogleDriveProviderInfo.Owner;
            file.CreateOn = TenantUtil.DateTimeNow();
            file.ModifiedBy = GoogleDriveProviderInfo.Owner;
            file.ModifiedOn = TenantUtil.DateTimeNow();
            file.ProviderId = GoogleDriveProviderInfo.ID;
            file.ProviderKey = GoogleDriveProviderInfo.ProviderKey;
            file.RootFolderCreator = GoogleDriveProviderInfo.Owner;
            file.RootFolderId = MakeId();
            file.RootFolderType = GoogleDriveProviderInfo.RootFolderType;
            file.Title = MakeFileTitle(driveEntry);
            file.Error = driveEntry.Error;

            return file;
        }

        private Folder<string> ToErrorFolder(ErrorDriveEntry driveEntry)
        {
            if (driveEntry == null) return null;
            var folder = ServiceProvider.GetService<Folder<string>>();

            folder.ID = MakeId(driveEntry.ErrorId);
            folder.ParentFolderID = null;
            folder.CreateBy = GoogleDriveProviderInfo.Owner;
            folder.CreateOn = TenantUtil.DateTimeNow();
            folder.FolderType = FolderType.DEFAULT;
            folder.ModifiedBy = GoogleDriveProviderInfo.Owner;
            folder.ModifiedOn = TenantUtil.DateTimeNow();
            folder.ProviderId = GoogleDriveProviderInfo.ID;
            folder.ProviderKey = GoogleDriveProviderInfo.ProviderKey;
            folder.RootFolderCreator = GoogleDriveProviderInfo.Owner;
            folder.RootFolderId = MakeId();
            folder.RootFolderType = GoogleDriveProviderInfo.RootFolderType;
            folder.Shareable = false;
            folder.Title = MakeFolderTitle(driveEntry);
            folder.TotalFiles = 0;
            folder.TotalSubFolders = 0;
            folder.Error = driveEntry.Error;

            return folder;
        }

        public File<string> ToFile(DriveFile driveFile)
        {
            if (driveFile == null) return null;

            if (driveFile is ErrorDriveEntry)
            {
                //Return error entry
                return ToErrorFile(driveFile as ErrorDriveEntry);
            }

            var file = ServiceProvider.GetService<File<string>>();

            file.ID = MakeId(driveFile.Id);
            file.Access = FileShare.None;
            file.ContentLength = driveFile.Size.HasValue ? (long)driveFile.Size : 0;
            file.CreateBy = GoogleDriveProviderInfo.Owner;
            file.CreateOn = driveFile.CreatedTime.HasValue ? TenantUtil.DateTimeFromUtc(driveFile.CreatedTime.Value) : default(DateTime);
            file.FileStatus = FileStatus.None;
            file.FolderID = MakeId(GetParentDriveId(driveFile));
            file.ModifiedBy = GoogleDriveProviderInfo.Owner;
            file.ModifiedOn = driveFile.ModifiedTime.HasValue ? TenantUtil.DateTimeFromUtc(driveFile.ModifiedTime.Value) : default(DateTime);
            file.NativeAccessor = driveFile;
            file.ProviderId = GoogleDriveProviderInfo.ID;
            file.ProviderKey = GoogleDriveProviderInfo.ProviderKey;
            file.Title = MakeFileTitle(driveFile);
            file.RootFolderId = MakeId();
            file.RootFolderType = GoogleDriveProviderInfo.RootFolderType;
            file.RootFolderCreator = GoogleDriveProviderInfo.Owner;
            file.Shared = false;
            file.Version = 1;

            return file;
        }

        public Folder<string> GetRootFolder(string folderId)
        {
            return ToFolder(GetDriveEntry(""));
        }

        protected DriveFile GetDriveEntry(string entryId)
        {
            var driveId = MakeDriveId(entryId);
            try
            {
                var entry = GoogleDriveProviderInfo.GetDriveEntry(driveId);
                return entry;
            }
            catch (Exception ex)
            {
                return new ErrorDriveEntry(ex, driveId);
            }
        }

        protected IEnumerable<string> GetChildren(object folderId)
        {
            return GetDriveEntries(folderId).Select(entry => MakeId(entry.Id));
        }

        protected List<DriveFile> GetDriveEntries(object parentId, bool? folder = null)
        {
            var parentDriveId = MakeDriveId(parentId);
            var entries = GoogleDriveProviderInfo.GetDriveEntries(parentDriveId, folder);
            return entries;
        }


        protected sealed class ErrorDriveEntry : DriveFile
        {
            public string Error { get; set; }

            public string ErrorId { get; private set; }


            public ErrorDriveEntry(Exception e, object id)
            {
                ErrorId = id.ToString();
                if (id.ToString() == "root")
                {
                    MimeType = GoogleLoginProvider.GoogleDriveMimeTypeFolder;
                }
                if (e != null)
                {
                    Error = e.Message;
                }
            }
        }
    }
}
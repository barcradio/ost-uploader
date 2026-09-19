using System.Globalization;
using System.Resources;

namespace ost_uploader
{
    public static class UiStrings
    {
        private static readonly ResourceManager ResourceManager =
            new("ost_uploader.Resources.Strings", typeof(UiStrings).Assembly);

        public static string App_ApplicationTitle => Get(nameof(App_ApplicationTitle));
        public static string Auth_Email => Get(nameof(Auth_Email));
        public static string Auth_Password => Get(nameof(Auth_Password));
        public static string Auth_Login => Get(nameof(Auth_Login));
        public static string Auth_SaveCredentials => Get(nameof(Auth_SaveCredentials));
        public static string Auth_SaveCredentialsTooltip => Get(nameof(Auth_SaveCredentialsTooltip));
        public static string Auth_NotAuthenticated => Get(nameof(Auth_NotAuthenticated));
        public static string Auth_AuthenticationFailed => Get(nameof(Auth_AuthenticationFailed));
        public static string Auth_Authenticated => Get(nameof(Auth_Authenticated));
        public static string Auth_AuthenticatedSaved => Get(nameof(Auth_AuthenticatedSaved));
        public static string Auth_TokenExpiration => Get(nameof(Auth_TokenExpiration));
        public static string Auth_LoginFailedTooltip => Get(nameof(Auth_LoginFailedTooltip));
        public static string Auth_AuthenticationTokenMissing => Get(nameof(Auth_AuthenticationTokenMissing));
        public static string Auth_PleaseAuthenticate => Get(nameof(Auth_PleaseAuthenticate));
        public static string Event_EventFileLabel => Get(nameof(Event_EventFileLabel));
        public static string Event_BrowseEventFile => Get(nameof(Event_BrowseEventFile));
        public static string Event_LoadEventZipStatus => Get(nameof(Event_LoadEventZipStatus));
        public static string Event_ProductionSwitchMessage => Get(nameof(Event_ProductionSwitchMessage));
        public static string Event_ConfirmProduction => Get(nameof(Event_ConfirmProduction));
        public static string Event_ErrorRetrievingEvent => Get(nameof(Event_ErrorRetrievingEvent));
        public static string Event_EventFileNotFound => Get(nameof(Event_EventFileNotFound));
        public static string Event_MissingOstMetadata => Get(nameof(Event_MissingOstMetadata));
        public static string Event_EventFileLoaded => Get(nameof(Event_EventFileLoaded));
        public static string Event_FailedLoadEvent => Get(nameof(Event_FailedLoadEvent));
        public static string Event_EventFileLoadFailed => Get(nameof(Event_EventFileLoadFailed));
        public static string Import_SelectCsvFile => Get(nameof(Import_SelectCsvFile));
        public static string Import_LoadFile => Get(nameof(Import_LoadFile));
        public static string Import_RecordsLoaded => Get(nameof(Import_RecordsLoaded));
        public static string Import_RecordsLoadedWithDuplicates => Get(nameof(Import_RecordsLoadedWithDuplicates));
        public static string Import_FailedReadCsvHeader => Get(nameof(Import_FailedReadCsvHeader));
        public static string Import_InvalidCsvHeader => Get(nameof(Import_InvalidCsvHeader));
        public static string Import_InvalidCsvFile => Get(nameof(Import_InvalidCsvFile));
        public static string Import_StationNotFound => Get(nameof(Import_StationNotFound));
        public static string Import_InvalidBib => Get(nameof(Import_InvalidBib));
        public static string Import_ReadyForUpload => Get(nameof(Import_ReadyForUpload));
        public static string Import_JsonDataMissing => Get(nameof(Import_JsonDataMissing));
        public static string Import_PleaseLoadTimes => Get(nameof(Import_PleaseLoadTimes));
        public static string Ost_Site => Get(nameof(Ost_Site));
        public static string Ost_Event => Get(nameof(Ost_Event));
        public static string Status_Ready => Get(nameof(Status_Ready));
        public static string Status_SplitKindSyncUnavailableNoSite => Get(nameof(Status_SplitKindSyncUnavailableNoSite));
        public static string Status_SplitKindSyncUnavailableError => Get(nameof(Status_SplitKindSyncUnavailableError));
        public static string Status_WaitingForAuthentication => Get(nameof(Status_WaitingForAuthentication));
        public static string Upload_Data => Get(nameof(Upload_Data));
        public static string Upload_ReadyColumn => Get(nameof(Upload_ReadyColumn));
        public static string Upload_ReadyYes => Get(nameof(Upload_ReadyYes));
        public static string Upload_ReadyNo => Get(nameof(Upload_ReadyNo));
        public static string Upload_Completed => Get(nameof(Upload_Completed));
        public static string Upload_ApiResponse => Get(nameof(Upload_ApiResponse));
        public static string Upload_Failed => Get(nameof(Upload_Failed));
        public static string Upload_FailedDetails => Get(nameof(Upload_FailedDetails));
        public static string Verification_Column => Get(nameof(Verification_Column));
        public static string Verification_DuplicateBib => Get(nameof(Verification_DuplicateBib));
        public static string Verification_DidNotStart => Get(nameof(Verification_DidNotStart));
        public static string Verification_DidNotStartAtStation => Get(nameof(Verification_DidNotStartAtStation));
        public static string Api_HttpError => Get(nameof(Api_HttpError));
        public static string Api_RequestError => Get(nameof(Api_RequestError));
        public static string Api_UnexpectedError => Get(nameof(Api_UnexpectedError));
        public static string Api_NoResponseBody => Get(nameof(Api_NoResponseBody));

        public static string Format(string resource, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, resource, arguments);

        private static string Get(string name) =>
            ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }
}

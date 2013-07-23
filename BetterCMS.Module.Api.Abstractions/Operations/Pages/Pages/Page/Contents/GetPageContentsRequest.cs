﻿using System.Runtime.Serialization;

using BetterCms.Module.Api.Infrastructure;

using ServiceStack.ServiceHost;

namespace BetterCms.Module.Api.Operations.Pages.Pages.Page.Contents
{
    [Route("/pages/{PageId}/contents")]
    [DataContract]
    public class GetPageContentsRequest : RequestBase<GetPageContentsModel>, IReturn<GetPageContentsResponse>
    {
    }
}
namespace OSDeploymentAssistant.Integrations
{
    /// <summary>
    /// Everything needed to raise one SCCM asset request in ServiceNow.
    /// </summary>
    public class ServiceNowRequestItem
    {
        public string RequestedFor { get; set; } = string.Empty;      // ServiceNow user_name/sys_id of the requester
        public string ShortDescription { get; set; } = string.Empty;
        public string CatalogItemSysId { get; set; } = string.Empty;  // sys_id of your "SCCM Asset" catalog item
        public string AssetName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
    }

    /// <summary>
    /// What comes back after the ticket is created.
    /// </summary>
    public class ServiceNowTicketResult
    {
        public string SysId { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty; // e.g. RITM0012345
    }

    public class ServiceNowException : System.Exception
    {
        public ServiceNowException(string message, System.Exception? inner = null) : base(message, inner) { }
    }
}
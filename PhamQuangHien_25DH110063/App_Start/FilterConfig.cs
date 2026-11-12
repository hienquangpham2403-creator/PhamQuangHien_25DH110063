using System.Web;
using System.Web.Mvc;

namespace PhamQuangHien_25DH110063
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace TwitchScanAPI.Controllers.Annotations
{
    public class AccessTokenAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) is not IConfiguration
                configuration)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            var requestToken = context.HttpContext.Request.Headers["AccessToken"].ToString();

            if (string.IsNullOrEmpty(requestToken))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace TwitchScanAPI.Controllers.Annotations
{
    public class MasterKeyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) is not IConfiguration configuration)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }
            
            var masterKey = configuration["MasterKey"];
            var requestKey = context.HttpContext.Request.Headers["MasterKey"].ToString();

            if (string.IsNullOrEmpty(masterKey))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            if (masterKey != requestKey)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
﻿/*----------------------------------------------------------------
    Copyright (C) 2016 Senparc

    文件名：OAuthContainer.cs
    文件功能描述：用户OAuth容器，用于自动管理OAuth的AccessToken，如果过期会重新获取


    创建标识：Senparc - 20160801

----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Senparc.Weixin.Containers;
using Senparc.Weixin.Exceptions;
using Senparc.Weixin.MP.Entities;
using Senparc.Weixin.CacheUtility;
using Senparc.Weixin.MP.AdvancedAPIs;
using Senparc.Weixin.MP.AdvancedAPIs.OAuth;
using Senparc.Weixin.MP.CommonAPIs;
using Senparc.Weixin.Utilities.WeixinUtility;

namespace Senparc.Weixin.MP.Containers
{
    /// <summary>
    /// OAuth包
    /// </summary>
    [Serializable]
    public class OAuthAccessTokenBag : BaseContainerBag
    {
        public string AppId
        {
            get { return _appId; }
            set { base.SetContainerProperty(ref _appId, value); }
        }
        public string AppSecret
        {
            get { return _appSecret; }
            set { base.SetContainerProperty(ref _appSecret, value); }
        }

        public OAuthAccessTokenResult OAuthAccessTokenResult
        {
            get { return _oAuthAccessTokenResult; }
            set { base.SetContainerProperty(ref _oAuthAccessTokenResult, value); }
        }

        public DateTime OAuthAccessTokenExpireTime
        {
            get { return _oAuthAccessTokenExpireTime; }
            set { base.SetContainerProperty(ref _oAuthAccessTokenExpireTime, value); }
        }

        /// <summary>
        /// 只针对这个AppId的锁
        /// </summary>
        internal object Lock = new object();

        private DateTime _oAuthAccessTokenExpireTime;
        private OAuthAccessTokenResult _oAuthAccessTokenResult;
        private string _appSecret;
        private string _appId;
    }

    /// <summary>
    /// 用户OAuth容器，用于自动管理OAuth的AccessToken，如果过期会重新获取（测试中，暂时别用）
    /// </summary>
    public class OAuthAccessTokenContainer : BaseContainer<OAuthAccessTokenBag>
    {
        //static Dictionary<string, JsApiTicketBag> JsApiTicketCollection =
        //   new Dictionary<string, JsApiTicketBag>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册应用凭证信息，此操作只是注册，不会马上获取Ticket，并将清空之前的Ticket，
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        /// <param name="name">标记JsApiTicket名称（如微信公众号名称），帮助管理员识别</param>
        public static void Register(string appId, string appSecret, string name = null)
        {
            using (FlushCache.CreateInstance())
            {
                Update(appId, new OAuthAccessTokenBag()
                {
                    Name = name,
                    AppId = appId,
                    AppSecret = appSecret,
                    OAuthAccessTokenExpireTime = DateTime.MinValue,
                    OAuthAccessTokenResult = new OAuthAccessTokenResult()
                });
            }
        }

        /// <summary>
        /// 返回已经注册的第一个AppId
        /// </summary>
        /// <returns></returns>
        public static string GetFirstOrDefaultAppId()
        {
            return ItemCollection.GetAll().Keys.FirstOrDefault();
        }

        #region OAuthAccessToken

        /// <summary>
        /// 使用完整的应用凭证获取Ticket，如果不存在将自动注册
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        /// <param name="code">code作为换取access_token的票据，每次用户授权带上的code将不一样，code只能使用一次，5分钟未被使用自动过期。</param>
        /// <param name="getNewToken"></param>
        /// <returns></returns>
        public static string TryGetOAuthAccessToken(string appId, string appSecret,string code, bool getNewToken = false)
        {
            if (!CheckRegistered(appId) || getNewToken)
            {
                Register(appId, appSecret);
            }
            return GetOAuthAccessToken(appId,code);
        }

        /// <summary>
        /// 获取可用Ticket
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="code">code作为换取access_token的票据，每次用户授权带上的code将不一样，code只能使用一次，5分钟未被使用自动过期。</param>
        /// <param name="getNewToken">是否强制重新获取新的Ticket</param>
        /// <returns></returns>
        public static string GetOAuthAccessToken(string appId, string code, bool getNewToken = false)
        {
            return GetOAuthAccessTokenResult(appId, code, getNewToken).access_token;
        }

        /// <summary>
        /// 获取可用Ticket
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="code">code作为换取access_token的票据，每次用户授权带上的code将不一样，code只能使用一次，5分钟未被使用自动过期。</param>
        /// <param name="getNewToken">是否强制重新获取新的Ticket</param>
        /// <returns></returns>
        public static OAuthAccessTokenResult GetOAuthAccessTokenResult(string appId, string code, bool getNewToken = false)
        {
            if (!CheckRegistered(appId))
            {
                throw new UnRegisterAppIdException(null, "此appId尚未注册，请先使用OAuthAccessTokenContainer.Register完成注册（全局执行一次即可）！");
            }

            var oAuthAccessTokenBag = (OAuthAccessTokenBag)ItemCollection[appId];
            lock (oAuthAccessTokenBag.Lock)
            {
                if (getNewToken || oAuthAccessTokenBag.OAuthAccessTokenExpireTime <= DateTime.Now)
                {
                    //已过期，重新获取
                    oAuthAccessTokenBag.OAuthAccessTokenResult = OAuthApi.GetAccessToken(oAuthAccessTokenBag.AppId, oAuthAccessTokenBag.AppSecret, code);
                    oAuthAccessTokenBag.OAuthAccessTokenExpireTime =
                        ApiUtility.GetExpireTime(oAuthAccessTokenBag.OAuthAccessTokenResult.expires_in);
                }
            }
            return oAuthAccessTokenBag.OAuthAccessTokenResult;
        }

        #endregion

    }
}

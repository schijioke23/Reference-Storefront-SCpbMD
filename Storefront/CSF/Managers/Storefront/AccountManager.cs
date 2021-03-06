﻿//-----------------------------------------------------------------------
// <copyright file="AccountManager.cs" company="Sitecore Corporation">
//     Copyright (c) Sitecore Corporation 1999-2014
// </copyright>
// <summary>The manager class responsible for encapsulating the account business logic for the site.</summary>
//-----------------------------------------------------------------------
// Copyright 2015 Sitecore Corporation A/S
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file 
// except in compliance with the License. You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the 
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
// either express or implied. See the License for the specific language governing permissions 
// and limitations under the License.
// -------------------------------------------------------------------------------------------

namespace Sitecore.Commerce.Storefront.Managers.Storefront
{
    using Sitecore.Analytics;
    using Sitecore.Commerce.Connect.CommerceServer;
    using Sitecore.Commerce.Connect.CommerceServer.Configuration;
    using Sitecore.Commerce.Connect.DynamicsRetail.Entities;
    using Sitecore.Commerce.Entities;
    using Sitecore.Commerce.Entities.Customers;
    using Sitecore.Commerce.Services.Customers;
    using Sitecore.Commerce.Storefront.Models.InputModels;
    using Sitecore.Commerce.Storefront.Models.SitecoreItemModels;
    using Sitecore.Diagnostics;
    using Sitecore.Security.Authentication;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Web.Security;

    /// <summary>
    /// Defines the AccountManager class.
    /// </summary>
    public class AccountManager : BaseManager
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountManager" /> class.
        /// </summary>
        /// <param name="cartManager">The cart manager.</param>
        /// <param name="customerServiceProvider">The customer service provider.</param>
        public AccountManager(CartManager cartManager, [NotNull] CustomerServiceProvider customerServiceProvider)
        {
            Assert.ArgumentNotNull(customerServiceProvider, "customerServiceProvider");

            this.CartManager = cartManager;
            this.CustomerServiceProvider = customerServiceProvider;
        }

        #endregion

        #region Properties (public)

        /// <summary>
        /// Gets or sets the cart manager.
        /// </summary>
        /// <value>
        /// The cart manager.
        /// </value>
        public CartManager CartManager { get; set; }

        /// <summary>
        /// Gets or sets the customer service provider.
        /// </summary>
        /// <value>
        /// The customer service provider.
        /// </value>
        public CustomerServiceProvider CustomerServiceProvider { get; protected set; }

        #endregion

        #region Methods (public, virtual)

        /// <summary>
        /// Logins the specified storefront.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="persistent">if set to <c>true</c> [persistent].</param>
        /// <returns>True if the user is logged in; Otherwise false.</returns>
        // TODO: Off pattern. Need to review
        public virtual bool Login([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, string userName, string password, bool persistent)
        {
            Assert.ArgumentNotNullOrEmpty(userName, "userName");
            Assert.ArgumentNotNullOrEmpty(password, "password");

            var isLoggedIn = AuthenticationManager.Login(userName, password, persistent);
            if (isLoggedIn)
            {
                string anonymousVisitorId = visitorContext.VisitorId;

                Tracker.Current.Session.Identify(userName);

                visitorContext.ResolveCommerceUser(this);

                this.CartManager.MergeCarts(storefront, visitorContext, anonymousVisitorId);
            }

            return isLoggedIn;
        }

        /// <summary>
        /// Logouts the current user.
        /// </summary>
        // TODO: Off pattern. Need to review
        public virtual void Logout()
        {
            Tracker.Current.EndVisit(true);
            System.Web.HttpContext.Current.Session.Abandon();
            AuthenticationManager.Logout();
        }

        /// <summary>
        /// Gets the user.
        /// </summary>
        /// <param name="userName">The username.</param>
        /// <returns>
        /// The manager response where the user is returned in the response.
        /// </returns>
        public virtual ManagerResponse<GetUserResult, CommerceUser> GetUser(string userName)
        {
            Assert.ArgumentNotNullOrEmpty(userName, "userName");

            var request = new GetUserRequest(userName);
            var result = this.CustomerServiceProvider.GetUser(request);

            return new ManagerResponse<GetUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <returns>The manager response where the success flag is returned in the result.</returns>
        public virtual ManagerResponse<DeleteUserResult, bool> DeleteUser([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");

            var userName = visitorContext.UserName;
            var commerceUser = this.GetUser(userName).Result;

            if (commerceUser != null)
            {
                // NOTE: we do not need to call DeleteCustomer because this will delete the commerce server user under the covers
                var request = new DeleteUserRequest(new CommerceUser { UserName = userName });
                var result = this.CustomerServiceProvider.DeleteUser(request);

                return new ManagerResponse<DeleteUserResult, bool>(result, result.Success);
            }

            return new ManagerResponse<DeleteUserResult, bool>(new DeleteUserResult() { Success = false }, false);
        }

        /// <summary>
        /// Updates the user.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="inputModel">The input model.</param>
        /// <returns>
        /// The manager response where the user is returned.
        /// </returns>
        public virtual ManagerResponse<UpdateUserResult, CommerceUser> UpdateUser([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, UpdateUserInputModel inputModel)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNull(inputModel, "inputModel");

            UpdateUserResult result;

            var userName = visitorContext.UserName;
            var commerceUser = this.GetUser(userName).Result;
            if (commerceUser != null)
            {
                commerceUser.FirstName = inputModel.FirstName;
                commerceUser.LastName = inputModel.LastName;
                commerceUser.Email = inputModel.Email;
                commerceUser.SetPropertyValue("Phone", inputModel.TelephoneNumber);

                try
                {
                    var request = new UpdateUserRequest(commerceUser);
                    result = this.CustomerServiceProvider.UpdateUser(request);
                }
                catch (Exception ex)
                {
                    result = new UpdateUserResult() { Success = false };

                    result.SystemMessages.Add(new Sitecore.Commerce.Services.SystemMessage() { Message = ex.Message + "/" + ex.StackTrace });
                }
            }
            else
            {
                // user is authenticated, but not in the CommerceUsers domain - probably here because we are in edit or preview mode
                var msg = string.Format(CultureInfo.InvariantCulture, "Cannot update profile details for user {0}.", Context.User.LocalName);
                result = new UpdateUserResult() { Success = false };

                result.SystemMessages.Add(new Commerce.Services.SystemMessage() { Message = msg });
            }

            return new ManagerResponse<UpdateUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Gets the user addresses.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="userName">Name of the user.</param>
        /// <returns>
        /// The manager response where the list of parties is returned in the response.
        /// </returns>
        public virtual ManagerResponse<GetPartiesResult, List<CommerceParty>> GetUserAddresses([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, [NotNull] string userName)
        {
            Assert.ArgumentNotNull(visitorContext, "visitorContext"); ;
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNullOrEmpty(userName, "userName");

            var response = this.GetUser(visitorContext.UserName);

            if (response.Result == null)
            {
                return new ManagerResponse<GetPartiesResult, List<CommerceParty>>(
                    new GetPartiesResult() { Success = true },
                    new List<CommerceParty>());
            }

            var partiesResponse = this.GetParties(storefront, new CommerceCustomer { ExternalId = response.Result.ExternalId });

            var partyList = (partiesResponse.Result).Cast<CommerceParty>().ToList();

            return new ManagerResponse<GetPartiesResult, List<CommerceParty>>(partiesResponse.ServiceProviderResult, partyList);
        }

        /// <summary>
        /// Gets the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <returns>The manager response where the list of parties is returned in the response.</returns>
        public virtual ManagerResponse<GetPartiesResult, IEnumerable<CommerceParty>> GetParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(user, "user");

            var request = new GetPartiesRequest(user);
            var result = this.CustomerServiceProvider.GetParties(request);

            var partyList = (result.Parties).Cast<CommerceParty>().ToList();

            return new ManagerResponse<GetPartiesResult, IEnumerable<CommerceParty>>(result, partyList);
        }

        /// <summary>
        /// Gets the current user parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <returns>The manager response where the list of parties is returned in the response.</returns>
        public virtual ManagerResponse<GetPartiesResult, IEnumerable<CommerceParty>> GetCurrentUserParties([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");

            var getUserResponse = this.GetUser(visitorContext.UserName);
            if (getUserResponse.Result == null)
            {
                var partiesResult = new GetPartiesResult { Success = false };
                foreach (var message in getUserResponse.ServiceProviderResult.SystemMessages)
                {
                    partiesResult.SystemMessages.Add(message);
                }

                return new ManagerResponse<GetPartiesResult, IEnumerable<CommerceParty>>(partiesResult, null);
            }

            var customer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };
            return this.GetParties(storefront, new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId });
        }

        /// <summary>
        /// Removes the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <param name="parties">The parties.</param>
        /// <returns>The manager result where the success flag is returned as the Result.</returns>
        public virtual ManagerResponse<CustomerResult, bool> RemoveParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user, List<CommerceParty> parties)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(user, "user");
            Assert.ArgumentNotNull(parties, "parties");

            var request = new RemovePartiesRequest(user, parties.Cast<Party>().ToList());
            var result = this.CustomerServiceProvider.RemoveParties(request);

            return new ManagerResponse<CustomerResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Removes the parties from current user.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="addressExternalId">The address external identifier.</param>
        /// <returns>
        /// The manager response with the successflag in the Result.
        /// </returns>
        public virtual ManagerResponse<CustomerResult, bool> RemovePartiesFromCurrentUser([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, [NotNull] string addressExternalId)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNullOrEmpty(addressExternalId, "addresseExternalId");

            var getUserResponse = this.GetUser(Context.User.Name);
            if (getUserResponse.Result == null)
            {
                var customerResult = new CustomerResult { Success = false };
                foreach (var message in getUserResponse.ServiceProviderResult.SystemMessages)
                {
                    customerResult.SystemMessages.Add(message);
                }

                return new ManagerResponse<CustomerResult, bool>(customerResult, false);
            }

            var customer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };
            var parties = new List<CommerceParty> { new CommerceParty { ExternalId = addressExternalId } };

            return this.RemoveParties(storefront, customer, parties);
        }

        /// <summary>
        /// Updates the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <param name="parties">The parties.</param>
        /// <returns>The manager result where the success flag is returned as the Result.</returns>
        public virtual ManagerResponse<CustomerResult, bool> UpdateParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user, List<CommerceParty> parties)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(user, "user");
            Assert.ArgumentNotNull(parties, "parties");

            var request = new UpdatePartiesRequest(user, parties.Cast<Party>().ToList());
            var result = this.CustomerServiceProvider.UpdateParties(request);

            return new ManagerResponse<CustomerResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Adds the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <param name="parties">The parties.</param>
        /// <returns>The manager result where the success flag is returned as the Result.</returns>
        public virtual ManagerResponse<AddPartiesResult, bool> AddParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user, List<CommerceParty> parties)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(user, "user");
            Assert.ArgumentNotNull(parties, "parties");

            var request = new AddPartiesRequest(user, parties.Cast<Party>().ToList());
            var result = this.CustomerServiceProvider.AddParties(request);

            return new ManagerResponse<AddPartiesResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Registers the user.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="inputModel">The input model.</param>
        /// <returns>
        /// The manager result where the user is returned as the Result.
        /// </returns>
        public virtual ManagerResponse<CreateUserResult, CommerceUser> RegisterUser([NotNull] CommerceStorefront storefront, RegisterUserInputModel inputModel)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(inputModel, "inputModel");
            Assert.ArgumentNotNullOrEmpty(inputModel.UserName, "inputModel.UserName");
            Assert.ArgumentNotNullOrEmpty(inputModel.Password, "inputModel.Password");

            CreateUserResult result;

            // Attempt to register the user
            try
            {
                var userName = UpdateUserName(inputModel.UserName);
                var shopName = Context.Site.Name;

                var request = new CreateUserRequest(inputModel.UserName, inputModel.Password, inputModel.UserName, storefront.ShopName);
                result = this.CustomerServiceProvider.CreateUser(request);
                if (!result.Success)
                {
                    Helpers.LogSystemMessages(result.SystemMessages, result);
                }
            }
            catch (MembershipCreateUserException e)
            {
                result = new CreateUserResult() { Success = false };
                result.SystemMessages.Add(new Commerce.Services.SystemMessage() { Message = ErrorCodeToString(e.StatusCode) });
            }

            return new ManagerResponse<CreateUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Updates the user password.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="inputModel">The input model.</param>
        /// <returns></returns>
        public virtual ManagerResponse<UpdatePasswordResult, bool> UpdateUserPassword([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, ChangePasswordInputModel inputModel)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNull(inputModel, "inputModel");
            Assert.ArgumentNotNullOrEmpty(inputModel.OldPassword, "inputModel.OldPassword");
            Assert.ArgumentNotNullOrEmpty(inputModel.NewPassword, "inputModel.NewPassword");

            var userName = visitorContext.UserName;

            var request = new UpdatePasswordRequest(userName, inputModel.OldPassword, inputModel.NewPassword);
            var result = this.CustomerServiceProvider.UpdatePassword(request);

            return new ManagerResponse<UpdatePasswordResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Sets the primary address.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="addressExternalId">The address external identifier.</param>
        /// <returns>The manager responsed with the success flag in the result.</returns>
        public virtual ManagerResponse<CustomerResult, bool> SetPrimaryAddress([NotNull] CommerceStorefront storefront, [NotNull] VisitorContext visitorContext, [NotNull]string addressExternalId)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNullOrEmpty(addressExternalId, "addressExternalId");

            var userPartiesResponse = this.GetCurrentUserParties(storefront, visitorContext);
            if (userPartiesResponse.ServiceProviderResult.Success)
            {
                var customerResult = new CustomerResult { Success = false };
                foreach (var message in userPartiesResponse.ServiceProviderResult.SystemMessages)
                {
                    customerResult.SystemMessages.Add(message);
                }

                return new ManagerResponse<CustomerResult, bool>(customerResult, false);
            }

            var addressesToUpdate = new List<CommerceParty>();

            var notPrimary = userPartiesResponse.Result.SingleOrDefault(address => address.IsPrimary);
            if (notPrimary != null)
            {
                notPrimary.IsPrimary = false;
                addressesToUpdate.Add(notPrimary);
            }

            var primaryAddress = userPartiesResponse.Result.Single(address => address.PartyId == addressExternalId);
            primaryAddress.IsPrimary = true;
            addressesToUpdate.Add(primaryAddress);

            var updatePartiesResponse = this.UpdateParties(storefront, new CommerceCustomer { ExternalId = visitorContext.UserId }, addressesToUpdate);

            return new ManagerResponse<CustomerResult, bool>(updatePartiesResponse.ServiceProviderResult, updatePartiesResponse.Result);
        }

        #endregion

        #region Methods (protected, virtual)

        /// <summary>
        /// Concats the user name with the current domain if it missing
        /// </summary>
        /// <param name="userName">The user's user name</param>
        /// <returns>The updated user name</returns>
        protected virtual string UpdateUserName(string userName)
        {
            var defaultDomain = CommerceServerSitecoreConfig.Current.DefaultCommerceUsersDomain;
            if (string.IsNullOrWhiteSpace(defaultDomain))
            {
                defaultDomain = CommerceConstants.ProfilesStrings.CommerceUsersDomainName;
            }

            return !userName.StartsWith(defaultDomain, StringComparison.OrdinalIgnoreCase) ? string.Concat(defaultDomain, @"\", userName) : userName;
        }

        /// <summary>
        /// Errors the code to string.
        /// </summary>
        /// <param name="createStatus">The create status.</param>
        /// <returns>The membership error status.</returns>
        protected virtual string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            // TODO: Hard code strings, please remove and apply localized values!
            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    return "User name already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }

        #endregion
    }
}
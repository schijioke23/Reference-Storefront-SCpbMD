﻿//-----------------------------------------------------------------------
// <copyright file="Storefront.cs" company="Sitecore Corporation">
//     Copyright (c) Sitecore Corporation 1999-2015
// </copyright>
// <summary>Defines the CommercePromotion class.</summary>
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

namespace Sitecore.Commerce.Storefront.Models.SitecoreItemModels
{
    using Sitecore.Data.Items;

    /// <summary>
    /// CommercePromotion class
    /// </summary>
    public class Catalog : SitecoreItemBase
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CommercePromotion"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        public Catalog(Item item)
        {
            Item = item;
        }

        /// <summary>
        /// The Name of the item
        /// </summary>
        public string Name
        {
            get { return Item.Name; }
        }

        /// <summary>
        /// The Title of the Create Wish List Page
        /// </summary>
        /// <returns></returns>
        public string Title()
        {
            return Item["Title"];
        }

        /// <summary>
        /// Label for the Wish List Name field
        /// </summary>
        /// <returns></returns>
        public string NameTitle()
        {
            return Item["Name Title"];
        }
    }
}
/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: RuntimeSessionProvider.cs 
*
* RuntimeSessionProvider.cs is part of SessionProvider which is part of the larger 
* VNLib collection of libraries and utilities.
*
* SessionProvider is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* SessionProvider is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Essentials.Sessions
{
    internal sealed class RuntimeSessionProvider(ISessionProvider externProvider) : ISessionProvider
    {
        private readonly Func<IHttpEvent, bool> _canProcessMethod = ManagedLibrary.GetMethod<Func<IHttpEvent, bool>>(externProvider, "CanProcess");
        private readonly ISessionProvider _ref = externProvider;

        ///<inheritdoc/>
        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken) => 
            _ref.GetSessionAsync(entity, cancellationToken);

        ///<inheritdoc/>
        public bool CanProcess(IHttpEvent ev) => _canProcessMethod(ev);
    }
}

/*
* Copyright (c) 2022 Vaughn Nugent
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

using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Net.Http;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Essentials.Sessions
{
    sealed class RuntimeSessionProvider : VnDisposeable, ISessionProvider
    {
        private readonly AssemblyLoader<ISessionProvider> _asm;
        
        private Func<IHttpEvent, bool> _canProcessMethod;
        private Action<PluginBase, ILogProvider> _loadMethod;
        private ISessionProvider _ref;

        public RuntimeSessionProvider(AssemblyLoader<ISessionProvider> asm)
        {
            _asm = asm;

            //Store ref to the resource to avoid loads
            _ref = asm.Resource;

            //Get load method
            _loadMethod = asm.TryGetMethod<Action<PluginBase, ILogProvider>>("Load")
                ?? throw new MissingMethodException("Provider is missing required Load method");

            //Load canprocess method
            _canProcessMethod = asm.TryGetMethod<Func<IHttpEvent, bool>>("CanProcess")
                ?? throw new MissingMethodException("Provider is missing required CanProcess method");
        }

        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _ref.GetSessionAsync(entity, cancellationToken);
        }

        public bool CanProcess(IHttpEvent ev)
        {
            return _canProcessMethod(ev);
        }

        public void Load(PluginBase pbase, ILogProvider localized)
        {
            _loadMethod(pbase, localized);
        }

        protected override void Free()
        {
            _asm.Dispose();
            _canProcessMethod = null!;
            _loadMethod = null!;
            _ref = null!;
        }
    }
}

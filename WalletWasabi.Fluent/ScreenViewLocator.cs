// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using ReactiveUI;

namespace WalletWasabi.Fluent
{
	public class ScreenViewLocator : IViewLocator
	{
		public IViewFor? ResolveView<T>(T viewModel, string? contract = null)
		{
			if (viewModel is { })
			{
				var name = viewModel.GetType().FullName!.Replace("ViewModel", "View");
				var type = Type.GetType(name);

				if (type != null)
				{
					return Activator.CreateInstance(type) as IViewFor ?? null;
				}
			}

			return null;
		}
	}
}
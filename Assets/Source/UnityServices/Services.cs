﻿using Unity.Services.Authentication;
using UnityEngine;

namespace Quinn.UnityServices
{
	public class Services : MonoBehaviour
	{
		private async void Start()
		{
			await Unity.Services.Core.UnityServices.Instance.InitializeAsync();
			await AuthenticationService.Instance.SignInAnonymouslyAsync();
		}
	}
}

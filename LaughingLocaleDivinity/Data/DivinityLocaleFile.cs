using LaughingLocale.ViewModel.Locale;
using System;
using System.Collections.Generic;
using System.Text;

namespace LaughingLocale.Divinity.Data
{
	public class DivinityLocaleFile : LocaleContainer
	{
		public LSLib.LS.Resource Resource { get; set; }

		public LSLib.LS.Enums.ResourceFormat Format { get; set; }
	}
}

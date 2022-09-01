using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// The basic report we get back from CoinVerifierAPI, within every necessary
///  information - and more - we need to verify a coin, mainly <see cref="CscoreSection"/>.
/// </summary>
public record ApiResponseItem(
	 ReportInfoSection Report_info_section,
	 CscoreSection Cscore_section,
	 ProfileSection Profile_section,

	 // FinancialAnalysisSection Financial_analysis_section,
	 OtherInformationSection Other_information_section
);

public record ReportInfoSection(
	string Report_id,
	string Version,
	string Address,
	string Address_type,
	string Address_subtype,
	string Asset,
	int Precision,
	string Report_type,
	DateTime Report_time,
	int Report_block_height,
	bool Address_used,
	bool Is_smart_contract,
	bool Whitelist,
	string Description
);

public record CscoreSection(
	int Cscore,
	string Description,
	List<CscoreInfo> Cscore_info
);

public record CscoreInfo(
	string Name,
	string Group_name,
	int Impact,
	int Id,
	int Display_priority
);

public record ProfileSection(Owner Owner);

public record Owner(
	string Name,
	string Url,
	List<Relation> Relations
);

public record Relation(
	string Name,
	string Url,
	string Type,
	string Legal_entity_name,
	string Description
);

// public record FinancialAnalysisSection(int Cc_balance);

public record IndicatorsSection(
	 string Cat_name,
	 bool Risk_detected,
	 bool Is_informative,
	 List<SubCategory> Sub_categories
);

public record SubCategory
(
	string Sub_cat_name,
	bool Risk_detected,
	bool Is_informative
);

public record OtherInformationSection
(
	 string Disclaimer,
	 List<Glossary> Glossary
);

public record Glossary
(
	string Term,
	string Description
);

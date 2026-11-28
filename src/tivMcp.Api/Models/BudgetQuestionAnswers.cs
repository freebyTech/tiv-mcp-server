using System.ComponentModel;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The answers to the questions the website's new budget wizard asks, for the
/// create_budget_from_answers tool. The trackItsValue API builds the budget's categories and
/// items from them. The names are the API's own (BudgetWizardQuestionReqeust), spelling included.
/// Generated from that class; regenerate rather than edit by hand.
/// </summary>
public class BudgetQuestionAnswers
{
  /// <summary>Whether the household receives paychecks.</summary>
  [JsonPropertyName("receivePaychecks")]
  [Description("True when the household receives paychecks.")]
  public bool ReceivePaychecks { get; set; }

  /// <summary>How many times a month, for receivePaychecks.</summary>
  [JsonPropertyName("paycheckCount")]
  [Description("How many times a month. Leave empty when receivePaychecks is false.")]
  public int? PaycheckCount { get; set; }

  /// <summary>The amount each time, for receivePaychecks.</summary>
  [JsonPropertyName("paycheckAmount")]
  [Description("The amount each time, as a positive number. Leave empty when receivePaychecks is false.")]
  public decimal? PaycheckAmount { get; set; }

  /// <summary>Whether the household lives on withdrawals from retirement funds.</summary>
  [JsonPropertyName("usesRetirementFunds")]
  [Description("True when the household lives on withdrawals from retirement funds.")]
  public bool UsesRetirementFunds { get; set; }

  /// <summary>How many times a month, for usesRetirementFunds.</summary>
  [JsonPropertyName("retirementWithdawlCount")]
  [Description("How many times a month. Leave empty when usesRetirementFunds is false.")]
  public int? RetirementWithdawlCount { get; set; }

  /// <summary>The amount each time, for usesRetirementFunds.</summary>
  [JsonPropertyName("retirementWithdawlAmount")]
  [Description("The amount each time, as a positive number. Leave empty when usesRetirementFunds is false.")]
  public decimal? RetirementWithdawlAmount { get; set; }

  /// <summary>Whether the household draws income from other accounts, such as savings.</summary>
  [JsonPropertyName("usesOtherAccounts")]
  [Description("True when the household draws income from other accounts, such as savings.")]
  public bool UsesOtherAccounts { get; set; }

  /// <summary>How many times a month, for usesOtherAccounts.</summary>
  [JsonPropertyName("accountWithdawlCount")]
  [Description("How many times a month. Leave empty when usesOtherAccounts is false.")]
  public int? AccountWithdawlCount { get; set; }

  /// <summary>The amount each time, for usesOtherAccounts.</summary>
  [JsonPropertyName("accountWithdawlAmount")]
  [Description("The amount each time, as a positive number. Leave empty when usesOtherAccounts is false.")]
  public decimal? AccountWithdawlAmount { get; set; }

  /// <summary>Whether the household buys groceries.</summary>
  [JsonPropertyName("payForGroceries")]
  [Description("True when the household buys groceries.")]
  public bool PayForGroceries { get; set; }

  /// <summary>The amount a month, for payForGroceries.</summary>
  [JsonPropertyName("groceriesAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payForGroceries is false.")]
  public decimal? GroceriesAmount { get; set; }

  /// <summary>Whether the household eats at restaurants.</summary>
  [JsonPropertyName("payForRestaurants")]
  [Description("True when the household eats at restaurants.")]
  public bool PayForRestaurants { get; set; }

  /// <summary>The amount a month, for payForRestaurants.</summary>
  [JsonPropertyName("restaurantsAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payForRestaurants is false.")]
  public decimal? RestaurantsAmount { get; set; }

  /// <summary>Whether the household pays a mortgage. When true, payRent must be false.</summary>
  [JsonPropertyName("payMortgage")]
  [Description("True when the household pays a mortgage. When true, payRent must be false.")]
  public bool PayMortgage { get; set; }

  /// <summary>Whether the household pays rent.</summary>
  [JsonPropertyName("payRent")]
  [Description("True when the household pays rent.")]
  public bool PayRent { get; set; }

  /// <summary>The amount a month, for payRent.</summary>
  [JsonPropertyName("housingAmount")]
  [Description("The mortgage or rent payment a month. Leave empty when payMortgage and payRent are both false.")]
  public decimal? HousingAmount { get; set; }

  /// <summary>Whether the household pays home or renter's insurance monthly. Only with a mortgage or rent.</summary>
  [JsonPropertyName("payMonthlyInsurance")]
  [Description("True when the household pays home or renter's insurance monthly. Only with a mortgage or rent.")]
  public bool PayMonthlyInsurance { get; set; }

  /// <summary>The amount a month, for payMonthlyInsurance.</summary>
  [JsonPropertyName("insuranceAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payMonthlyInsurance is false.")]
  public decimal? InsuranceAmount { get; set; }

  /// <summary>Whether the household pays any utilities. When false, the electric, gas, water and garbage answers must be false.</summary>
  [JsonPropertyName("payUtilities")]
  [Description("True when the household pays any utilities. When false, the electric, gas, water and garbage answers must be false.")]
  public bool PayUtilities { get; set; }

  /// <summary>Whether the household pays an electric bill.</summary>
  [JsonPropertyName("payElectric")]
  [Description("True when the household pays an electric bill.")]
  public bool PayElectric { get; set; }

  /// <summary>The amount a month, for payElectric.</summary>
  [JsonPropertyName("electricAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payElectric is false.")]
  public decimal? ElectricAmount { get; set; }

  /// <summary>Whether the household pays a gas (heating) bill.</summary>
  [JsonPropertyName("payGas")]
  [Description("True when the household pays a gas (heating) bill.")]
  public bool PayGas { get; set; }

  /// <summary>The amount a month, for payGas.</summary>
  [JsonPropertyName("gasAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payGas is false.")]
  public decimal? GasAmount { get; set; }

  /// <summary>Whether the household pays a water bill.</summary>
  [JsonPropertyName("payWater")]
  [Description("True when the household pays a water bill.")]
  public bool PayWater { get; set; }

  /// <summary>The amount a month, for payWater.</summary>
  [JsonPropertyName("waterAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payWater is false.")]
  public decimal? WaterAmount { get; set; }

  /// <summary>Whether the household pays for garbage collection.</summary>
  [JsonPropertyName("payGarbage")]
  [Description("True when the household pays for garbage collection.")]
  public bool PayGarbage { get; set; }

  /// <summary>The amount a month, for payGarbage.</summary>
  [JsonPropertyName("garbageAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payGarbage is false.")]
  public decimal? GarbageAmount { get; set; }

  /// <summary>Whether the household pays for cable or satellite television.</summary>
  [JsonPropertyName("payCableOrSatellite")]
  [Description("True when the household pays for cable or satellite television.")]
  public bool PayCableOrSatellite { get; set; }

  /// <summary>The amount a month, for payCableOrSatellite.</summary>
  [JsonPropertyName("cableOrSatelliteAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payCableOrSatellite is false.")]
  public decimal? CableOrSatelliteAmount { get; set; }

  /// <summary>Whether the household pays for cell phones.</summary>
  [JsonPropertyName("payCellPhone")]
  [Description("True when the household pays for cell phones.")]
  public bool PayCellPhone { get; set; }

  /// <summary>The amount a month, for payCellPhone.</summary>
  [JsonPropertyName("cellPhoneAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payCellPhone is false.")]
  public decimal? CellPhoneAmount { get; set; }

  /// <summary>Whether the household pays for streaming services.</summary>
  [JsonPropertyName("payStreaming")]
  [Description("True when the household pays for streaming services.")]
  public bool PayStreaming { get; set; }

  /// <summary>The amount a month, for payStreaming.</summary>
  [JsonPropertyName("streamingAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payStreaming is false.")]
  public decimal? StreamingAmount { get; set; }

  /// <summary>Whether the household pays for internet.</summary>
  [JsonPropertyName("payInternet")]
  [Description("True when the household pays for internet.")]
  public bool PayInternet { get; set; }

  /// <summary>The amount a month, for payInternet.</summary>
  [JsonPropertyName("internetAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payInternet is false.")]
  public decimal? InternetAmount { get; set; }

  /// <summary>Whether the household has fuel and car running costs.</summary>
  [JsonPropertyName("gasAndCarExpenses")]
  [Description("True when the household has fuel and car running costs.")]
  public bool GasAndCarExpenses { get; set; }

  /// <summary>The amount a month, for gasAndCarExpenses.</summary>
  [JsonPropertyName("gasAndCarExpensesAmount")]
  [Description("The amount a month, as a positive number. Leave empty when gasAndCarExpenses is false.")]
  public decimal? GasAndCarExpensesAmount { get; set; }

  /// <summary>Whether the household pays a car loan.</summary>
  [JsonPropertyName("carLoan")]
  [Description("True when the household pays a car loan.")]
  public bool CarLoan { get; set; }

  /// <summary>The amount a month, for carLoan.</summary>
  [JsonPropertyName("carLoanAmount")]
  [Description("The amount a month, as a positive number. Leave empty when carLoan is false.")]
  public decimal? CarLoanAmount { get; set; }

  /// <summary>Whether the household pays car insurance.</summary>
  [JsonPropertyName("carInsurance")]
  [Description("True when the household pays car insurance.")]
  public bool CarInsurance { get; set; }

  /// <summary>The amount a month, for carInsurance.</summary>
  [JsonPropertyName("carInsuranceAmount")]
  [Description("The amount a month, as a positive number. Leave empty when carInsurance is false.")]
  public decimal? CarInsuranceAmount { get; set; }

  /// <summary>Whether the household pays for the bus or other transport.</summary>
  [JsonPropertyName("busOrOtherExpenses")]
  [Description("True when the household pays for the bus or other transport.")]
  public bool BusOrOtherExpenses { get; set; }

  /// <summary>The amount a month, for busOrOtherExpenses.</summary>
  [JsonPropertyName("busOrOtherExpensesAmount")]
  [Description("The amount a month, as a positive number. Leave empty when busOrOtherExpenses is false.")]
  public decimal? BusOrOtherExpensesAmount { get; set; }

  /// <summary>Whether the household pays medical insurance.</summary>
  [JsonPropertyName("medicalInsurance")]
  [Description("True when the household pays medical insurance.")]
  public bool MedicalInsurance { get; set; }

  /// <summary>The amount a month, for medicalInsurance.</summary>
  [JsonPropertyName("medicalInsuranceAmount")]
  [Description("The amount a month, as a positive number. Leave empty when medicalInsurance is false.")]
  public decimal? MedicalInsuranceAmount { get; set; }

  /// <summary>Whether the household has other medical costs.</summary>
  [JsonPropertyName("otherMedicalExpenses")]
  [Description("True when the household has other medical costs.")]
  public bool OtherMedicalExpenses { get; set; }

  /// <summary>The amount a month, for otherMedicalExpenses.</summary>
  [JsonPropertyName("otherMedicalExpensesAmount")]
  [Description("The amount a month, as a positive number. Leave empty when otherMedicalExpenses is false.")]
  public decimal? OtherMedicalExpensesAmount { get; set; }

  /// <summary>Whether the household pays for childcare.</summary>
  [JsonPropertyName("childcareServices")]
  [Description("True when the household pays for childcare.")]
  public bool ChildcareServices { get; set; }

  /// <summary>The amount a month, for childcareServices.</summary>
  [JsonPropertyName("childcareAmount")]
  [Description("The amount a month, as a positive number. Leave empty when childcareServices is false.")]
  public decimal? ChildcareAmount { get; set; }

  /// <summary>Whether the household pays school tuition for a child.</summary>
  [JsonPropertyName("payChildTuition")]
  [Description("True when the household pays school tuition for a child.")]
  public bool PayChildTuition { get; set; }

  /// <summary>The amount a month, for payChildTuition.</summary>
  [JsonPropertyName("childTuitionAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payChildTuition is false.")]
  public decimal? ChildTuitionAmount { get; set; }

  /// <summary>Whether the household has other essential costs.</summary>
  [JsonPropertyName("otherEssentials")]
  [Description("True when the household has other essential costs.")]
  public bool OtherEssentials { get; set; }

  /// <summary>The amount a month, for otherEssentials.</summary>
  [JsonPropertyName("otherEssentialsAmount")]
  [Description("The amount a month, as a positive number. Leave empty when otherEssentials is false.")]
  public decimal? OtherEssentialsAmount { get; set; }

  /// <summary>Whether the household sets money aside for entertainment.</summary>
  [JsonPropertyName("entertainmentMoney")]
  [Description("True when the household sets money aside for entertainment.")]
  public bool EntertainmentMoney { get; set; }

  /// <summary>The amount a month, for entertainmentMoney.</summary>
  [JsonPropertyName("entertainmentAmount")]
  [Description("The amount a month, as a positive number. Leave empty when entertainmentMoney is false.")]
  public decimal? EntertainmentAmount { get; set; }

  /// <summary>Whether the household gives to a church.</summary>
  [JsonPropertyName("payChurch")]
  [Description("True when the household gives to a church.")]
  public bool PayChurch { get; set; }

  /// <summary>The amount a month, for payChurch.</summary>
  [JsonPropertyName("churchAmount")]
  [Description("The amount a month, as a positive number. Leave empty when payChurch is false.")]
  public decimal? ChurchAmount { get; set; }

  /// <summary>Whether the household gives to other charities.</summary>
  [JsonPropertyName("otherCharity")]
  [Description("True when the household gives to other charities.")]
  public bool OtherCharity { get; set; }

  /// <summary>The amount a month, for otherCharity.</summary>
  [JsonPropertyName("otherCharityAmount")]
  [Description("The amount a month, as a positive number. Leave empty when otherCharity is false.")]
  public decimal? OtherCharityAmount { get; set; }
}

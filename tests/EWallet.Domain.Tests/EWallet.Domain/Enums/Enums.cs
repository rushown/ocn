namespace EWallet.Domain.Enums;

public enum TransactionType   { Deposit, Withdrawal, Transfer }
public enum TransactionStatus { Pending, Completed, Failed, Refunded }
public enum TransactionState  { Pending, Completed, Failed, Refunded }
public enum KycLevel          { Unverified = 0, Tier1 = 1, Tier2 = 2, Tier3 = 3 }

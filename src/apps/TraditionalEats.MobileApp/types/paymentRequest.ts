/**
 * Payment Request Type
 * Sent by vendor in chat to request custom payment from customer
 */

export interface PaymentRequestMetadata {
  type: "payment_request";
  amount: number;
  description?: string;
  status: "pending" | "accepted" | "rejected";
  createdAt: string;
}

export function isPaymentRequest(metadataJson?: string): metadataJson is string {
  if (!metadataJson) return false;
  try {
    const metadata = JSON.parse(metadataJson) as PaymentRequestMetadata;
    return metadata.type === "payment_request";
  } catch {
    return false;
  }
}

export function parsePaymentRequest(metadataJson: string): PaymentRequestMetadata | null {
  try {
    const metadata = JSON.parse(metadataJson) as PaymentRequestMetadata;
    if (metadata.type === "payment_request") {
      return metadata;
    }
  } catch {
    return null;
  }
  return null;
}

export function createPaymentRequestMetadata(
  amount: number,
  description?: string,
): PaymentRequestMetadata {
  return {
    type: "payment_request",
    amount,
    description: description || undefined,
    status: "pending",
    createdAt: new Date().toISOString(),
  };
}

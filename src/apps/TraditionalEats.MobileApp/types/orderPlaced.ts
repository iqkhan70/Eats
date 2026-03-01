/**
 * Order Placed metadata type
 * Pushed into vendor chat when a customer places an order
 */

export interface OrderPlacedItem {
  menuItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  modifiers: string[];
}

export interface OrderPlacedMetadata {
  type: "order_placed";
  orderId: string;
  items: OrderPlacedItem[];
  total: number;
  serviceFee: number;
  deliveryAddress: string;
  placedAt: string;
}

export function isOrderPlaced(metadataJson?: string): metadataJson is string {
  if (!metadataJson) return false;
  try {
    const metadata = JSON.parse(metadataJson) as { type?: string };
    return metadata.type === "order_placed";
  } catch {
    return false;
  }
}

export function parseOrderPlaced(
  metadataJson: string,
): OrderPlacedMetadata | null {
  try {
    const metadata = JSON.parse(metadataJson) as OrderPlacedMetadata;
    if (metadata.type === "order_placed") {
      return metadata;
    }
  } catch {
    return null;
  }
  return null;
}

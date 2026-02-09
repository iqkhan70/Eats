import React from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  FlatList,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import ReviewRating from "./ReviewRating";

export interface Review {
  reviewId: string;
  orderId: string;
  userId: string;
  restaurantId: string;
  rating: number;
  comment?: string;
  tags?: string[];
  response?: string;
  responseAt?: string;
  isVerified: boolean;
  isVisible: boolean;
  createdAt: string;
  updatedAt: string;
}

interface ReviewDisplayProps {
  reviews: Review[];
}

export default function ReviewDisplay({ reviews }: ReviewDisplayProps) {
  if (!reviews || reviews.length === 0) {
    return (
      <View style={styles.emptyContainer}>
        <Ionicons name="rate-review-outline" size={48} color="#999" />
        <Text style={styles.emptyText}>
          No reviews yet. Be the first to review!
        </Text>
      </View>
    );
  }

  return (
    <FlatList
      data={reviews}
      keyExtractor={(item) => item.reviewId}
      renderItem={({ item }) => <ReviewCard review={item} />}
      scrollEnabled={false}
    />
  );
}

function ReviewCard({ review }: { review: Review }) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
    });
  };

  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <View style={styles.ratingContainer}>
          <ReviewRating
            value={review.rating}
            editable={false}
            showValue={true}
            size={22}
          />
          {review.isVerified && (
            <View style={styles.verifiedBadge}>
              <Ionicons name="checkmark-circle" size={16} color="#2e7d32" />
              <Text style={styles.verifiedText}>Verified</Text>
            </View>
          )}
        </View>
        <Text style={styles.dateText}>{formatDate(review.createdAt)}</Text>
      </View>

      {review.comment && (
        <Text style={styles.comment}>{review.comment}</Text>
      )}

      {review.tags && review.tags.length > 0 && (
        <View style={styles.tagsContainer}>
          {review.tags.map((tag, index) => (
            <View key={index} style={styles.tag}>
              <Text style={styles.tagText}>{tag}</Text>
            </View>
          ))}
        </View>
      )}

      {review.response && (
        <View style={styles.responseContainer}>
          <Text style={styles.responseLabel}>Restaurant Response</Text>
          <Text style={styles.responseText}>{review.response}</Text>
          {review.responseAt && (
            <Text style={styles.responseDate}>
              {formatDate(review.responseAt)}
            </Text>
          )}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  emptyContainer: {
    alignItems: "center",
    justifyContent: "center",
    padding: 32,
  },
  emptyText: {
    marginTop: 12,
    fontSize: 14,
    color: "#999",
    textAlign: "center",
  },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    marginBottom: 8,
  },
  ratingContainer: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    flex: 1,
    flexWrap: "wrap",
  },
  verifiedBadge: {
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
    backgroundColor: "#e8f5e9",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  verifiedText: {
    fontSize: 12,
    color: "#2e7d32",
    fontWeight: "500",
  },
  dateText: {
    fontSize: 12,
    color: "#999",
  },
  comment: {
    fontSize: 14,
    color: "#333",
    marginTop: 8,
    lineHeight: 20,
  },
  tagsContainer: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
    marginTop: 12,
  },
  tag: {
    backgroundColor: "#e3f2fd",
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  tagText: {
    fontSize: 12,
    color: "#1976d2",
  },
  responseContainer: {
    backgroundColor: "#f5f5f5",
    borderRadius: 8,
    padding: 12,
    marginTop: 12,
  },
  responseLabel: {
    fontSize: 12,
    fontWeight: "600",
    color: "#667eea",
    marginBottom: 4,
  },
  responseText: {
    fontSize: 13,
    color: "#333",
    marginBottom: 4,
  },
  responseDate: {
    fontSize: 11,
    color: "#999",
  },
});

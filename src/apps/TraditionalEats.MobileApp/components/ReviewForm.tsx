import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Alert,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import ReviewRating from "./ReviewRating";
import { api } from "../services/api";
import { authService } from "../services/auth";

interface ReviewFormProps {
  orderId: string;
  restaurantId: string;
  existingReviewId?: string;
  onReviewSubmitted?: () => void;
  onCancel?: () => void;
}

const AVAILABLE_TAGS = [
  "Fast Delivery",
  "Great Food",
  "Good Value",
  "Friendly Service",
  "Clean Restaurant",
  "Accurate Order",
];

export default function ReviewForm({
  orderId,
  restaurantId,
  existingReviewId,
  onReviewSubmitted,
  onCancel,
}: ReviewFormProps) {
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [selectedTags, setSelectedTags] = useState<Set<string>>(new Set());
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (existingReviewId) {
      loadExistingReview();
    }
  }, [existingReviewId]);

  const loadExistingReview = async () => {
    try {
      setLoading(true);
      const token = await authService.getAccessToken();
      if (!token) {
        Alert.alert("Error", "Please log in to view your review");
        return;
      }

      const response = await api.get(`/MobileBff/reviews/${existingReviewId}`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.data) {
        setRating(response.data.rating);
        setComment(response.data.comment || "");
        setSelectedTags(new Set(response.data.tags || []));
      }
    } catch (error: any) {
      console.error("Error loading review:", error);
      Alert.alert("Error", "Failed to load review");
    } finally {
      setLoading(false);
    }
  };

  const toggleTag = (tag: string) => {
    const newTags = new Set(selectedTags);
    if (newTags.has(tag)) {
      newTags.delete(tag);
    } else {
      newTags.add(tag);
    }
    setSelectedTags(newTags);
  };

  const handleSubmit = async () => {
    if (rating < 1 || rating > 5) {
      Alert.alert("Error", "Please select a rating");
      return;
    }

    try {
      setIsSubmitting(true);
      const token = await authService.getAccessToken();
      if (!token) {
        Alert.alert("Error", "Please log in to submit a review");
        return;
      }

      const selectedTagsArray = Array.from(selectedTags);

      if (existingReviewId) {
        // Update existing review
        const response = await api.put(
          `/MobileBff/reviews/${existingReviewId}`,
          {
            rating: rating,
            comment: comment,
            tags: selectedTagsArray,
          },
          {
            headers: { Authorization: `Bearer ${token}` },
          }
        );

        if (response.status === 200) {
          Alert.alert("Success", "Review updated successfully");
          onReviewSubmitted?.();
        }
      } else {
        // Create new review
        const response = await api.post(
          "/MobileBff/reviews",
          {
            orderId,
            restaurantId,
            review: {
              rating,
              comment,
              tags: selectedTagsArray,
            },
          },
          {
            headers: { Authorization: `Bearer ${token}` },
          }
        );

        if (response.status === 200) {
          Alert.alert("Success", "Review submitted successfully");
          // Reset form
          setRating(5);
          setComment("");
          setSelectedTags(new Set());
          onReviewSubmitted?.();
        }
      }
    } catch (error: any) {
      console.error("Error submitting review:", error);
      Alert.alert(
        "Error",
        error.response?.data?.message || "Failed to submit review"
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#667eea" />
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.title}>
        {existingReviewId ? "Edit Review" : "Write a Review"}
      </Text>

      <View style={styles.section}>
        <Text style={styles.label}>Rating</Text>
        <ReviewRating
          value={rating}
          editable={true}
          showValue={true}
          size={28}
          onRatingChange={setRating}
        />
      </View>

      <View style={styles.section}>
        <Text style={styles.label}>Comment (optional)</Text>
        <TextInput
          style={styles.textInput}
          placeholder="Share your experience..."
          placeholderTextColor="#999"
          multiline
          numberOfLines={4}
          value={comment}
          onChangeText={setComment}
          textAlignVertical="top"
        />
      </View>

      <View style={styles.section}>
        <Text style={styles.label}>Tags (optional)</Text>
        <View style={styles.tagsContainer}>
          {AVAILABLE_TAGS.map((tag) => (
            <TouchableOpacity
              key={tag}
              style={[
                styles.tag,
                selectedTags.has(tag) && styles.tagSelected,
              ]}
              onPress={() => toggleTag(tag)}
            >
              <Text
                style={[
                  styles.tagText,
                  selectedTags.has(tag) && styles.tagTextSelected,
                ]}
              >
                {tag}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      <View style={styles.buttonContainer}>
        {existingReviewId && onCancel && (
          <TouchableOpacity
            style={[styles.button, styles.cancelButton]}
            onPress={onCancel}
            disabled={isSubmitting}
          >
            <Text style={styles.cancelButtonText}>Cancel</Text>
          </TouchableOpacity>
        )}
        <TouchableOpacity
          style={[
            styles.button,
            styles.submitButton,
            (rating < 1 || rating > 5) && styles.submitButtonDisabled,
          ]}
          onPress={handleSubmit}
          disabled={isSubmitting || rating < 1 || rating > 5}
        >
          {isSubmitting ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <>
              <Ionicons name="send" size={18} color="#fff" />
              <Text style={styles.submitButtonText}>
                {existingReviewId ? "Update Review" : "Submit Review"}
              </Text>
            </>
          )}
        </TouchableOpacity>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#fff",
  },
  loadingContainer: {
    padding: 32,
    alignItems: "center",
    justifyContent: "center",
  },
  title: {
    fontSize: 20,
    fontWeight: "bold",
    marginBottom: 20,
    color: "#333",
  },
  section: {
    marginBottom: 24,
  },
  label: {
    fontSize: 14,
    fontWeight: "500",
    marginBottom: 8,
    color: "#333",
  },
  textInput: {
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    minHeight: 100,
    color: "#333",
  },
  tagsContainer: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
  },
  tag: {
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 20,
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: "#fff",
  },
  tagSelected: {
    backgroundColor: "#667eea",
    borderColor: "#667eea",
  },
  tagText: {
    fontSize: 12,
    color: "#666",
  },
  tagTextSelected: {
    color: "#fff",
  },
  buttonContainer: {
    flexDirection: "row",
    gap: 12,
    marginTop: 8,
    marginBottom: 24,
  },
  button: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    paddingVertical: 12,
    borderRadius: 8,
    gap: 8,
  },
  cancelButton: {
    backgroundColor: "#f5f5f5",
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  cancelButtonText: {
    fontSize: 14,
    fontWeight: "500",
    color: "#666",
  },
  submitButton: {
    backgroundColor: "#667eea",
  },
  submitButtonDisabled: {
    backgroundColor: "#ccc",
  },
  submitButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#fff",
  },
});

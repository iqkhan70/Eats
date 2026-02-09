import React from "react";
import { View, Text, StyleSheet, TouchableOpacity } from "react-native";
import { Ionicons } from "@expo/vector-icons";

interface ReviewRatingProps {
  value: number;
  editable?: boolean;
  showValue?: boolean;
  size?: number;
  onRatingChange?: (rating: number) => void;
}

export default function ReviewRating({
  value,
  editable = false,
  showValue = true,
  size = 20,
  onRatingChange,
}: ReviewRatingProps) {
  const handlePress = (rating: number) => {
    if (editable && onRatingChange) {
      onRatingChange(rating);
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.stars}>
        {[1, 2, 3, 4, 5].map((star) => (
          <TouchableOpacity
            key={star}
            onPress={() => handlePress(star)}
            disabled={!editable}
            activeOpacity={editable ? 0.5 : 1}
            hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
            style={editable ? styles.starButton : undefined}
          >
            <Ionicons
              name={star <= value ? "star" : "star-outline"}
              size={size}
              color={star <= value ? "#FFD700" : editable ? "#999" : "#d0d0d0"}
            />
          </TouchableOpacity>
        ))}
      </View>
      {showValue && (
        <Text style={styles.valueText}>
          {value.toFixed(1)} / 5
        </Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
  },
  stars: {
    flexDirection: "row",
    gap: 4,
  },
  starButton: {
    padding: 4,
  },
  valueText: {
    marginLeft: 8,
    fontWeight: "500",
    fontSize: 14,
    color: "#333",
  },
});

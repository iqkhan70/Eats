-- Optional: Clear existing items
SET @restaurant_id = UUID();
SET @owner_id = '926f872c-129d-42c1-8c78-02f4b6f23936';
SET @food_cat = 'c931f5f8-dd31-492a-9764-24ab1cbd1344';
SET @now = UTC_TIMESTAMP(6);

INSERT INTO traditional_eats_restaurant.Restaurants
  (RestaurantId, OwnerId, Name, Description, CuisineType, Address, Latitude, Longitude,
   PhoneNumber, Email, IsActive, Rating, ReviewCount, CreatedAt, UpdatedAt, VendorType)
VALUES
  (@restaurant_id, @owner_id, 'Yafa Mediterranean',
   'Authentic Mediterranean cuisine featuring shawarma, kabobs, gyros, and fresh salads.',
   'Mediterranean', '13475 Switzer Rd, Overland Park, KS 66213',
   38.8816, -94.7095,
   '+19133874623', 'yafamediterranean@gmail.com',
   1, 0, 0, @now, @now, 'Food');


SELECT @restaurant_id AS NewRestaurantId;
-- Insert new menu
INSERT INTO traditional_eats_catalog.MenuItems
(MenuItemId, RestaurantId, CategoryId, Name, Description, Price, IsAvailable, DietaryTagsJson, CreatedAt, UpdatedAt)
VALUES
-- === STARTERS ===
(UUID(), @restaurant_id, @food_cat, 'Hummus',
 'Chickpeas, tahini, garlic, olive oil, lemon. Served with pita.', 8.50, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Falafel',
 'Chickpea patties with herbs and spices. Served with tahini.', 7.50, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Baba Ghanouj',
 'Roasted eggplant dip with tahini and lemon.', 8.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Stuffed Grape Leaves',
 'Rice and vegetables wrapped in grape leaves.', 6.50, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Spanakopita',
 'Phyllo pastry with spinach and cheese.', 7.50, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Kibbeh',
 'Bulgur wheat with ground beef and spices.', 8.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Shrimp Garlic',
 'Grilled shrimp with garlic parsley sauce.', 8.50, 1, '[]', @now, @now),

-- === SOUPS ===
(UUID(), @restaurant_id, @food_cat, 'Lentil Soup (Cup)',
 'Pureed lentils with spices and lemon.', 6.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Lentil Soup (Bowl)',
 'Pureed lentils with spices and lemon.', 8.00, 1, '["Vegetarian"]', @now, @now),

-- === SALADS ===
(UUID(), @restaurant_id, @food_cat, 'Yafa Salad',
 'Cucumber, tomato, onion, mint, lemon dressing.', 8.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Tabbouleh Salad',
 'Parsley, tomato, bulgur, olive oil, lemon.', 8.50, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Jerusalem Salad',
 'Cucumber, tomato, onion, herbs, tahini.', 8.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Greek Salad (Small)',
 'Greens, olives, feta, cucumber, tomato.', 9.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Greek Salad (Large)',
 'Greens, olives, feta, cucumber, tomato.', 11.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Grilled Salmon Greek Salad',
 'Greek salad topped with grilled salmon.', 17.00, 1, '[]', @now, @now),

-- === SANDWICHES ===
(UUID(), @restaurant_id, @food_cat, 'Beef & Lamb Gyro Sandwich',
 'Gyro in pita with lettuce, tomato, tzatziki.', 12.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Beef or Lamb Shawarma Sandwich',
 'Shawarma wrap with pickles, onions, tahini.', 14.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Chicken Shawarma Sandwich',
 'Chicken shawarma wrap with vegetables.', 13.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Beef Mansaf Sandwich',
 'Shredded beef with rice and yogurt sauce.', 13.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Kufta Kabob Sandwich',
 'Ground beef kabob in pita.', 13.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Falafel Sandwich',
 'Falafel with tahini in pita.', 11.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Falafel & Hummus Sandwich',
 'Falafel and hummus in pita.', 12.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Grilled Veggie Sandwich',
 'Roasted vegetables with tzatziki.', 12.00, 1, '["Vegetarian"]', @now, @now),

-- === ENTREES ===
(UUID(), @restaurant_id, @food_cat, 'Gyro Plate',
 'Gyro over rice with tzatziki.', 14.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Vegetarian Plate',
 'Falafel, hummus, baba ghanouj, rice.', 14.00, 1, '["Vegetarian"]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Chicken Biryani Plate',
 'Chicken biryani with rice.', 14.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Beef Biryani Plate',
 'Beef biryani with rice.', 16.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Lamb Biryani Plate',
 'Lamb biryani with rice.', 16.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Lamb Mansaf Plate',
 'Lamb with rice and yogurt sauce.', 18.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Creamy Chicken Plate',
 'Chicken with creamy garlic sauce.', 15.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Yafa Combo Plate',
 'Combo of hummus, gyro or shawarma, rice.', 15.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Chicken Curry',
 'Chicken in creamy curry sauce.', 14.50, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Grilled Salmon Plate',
 'Grilled salmon with rice and vegetables.', 18.00, 1, '[]', @now, @now),

(UUID(), @restaurant_id, @food_cat, 'Shawarma Plate',
 'Chicken shawarma over rice.', 14.00, 1, '[]', @now, @now),

-- === KABOBS ===
(UUID(), @restaurant_id, @food_cat, 'Beef Kabob Plate', NULL, 17.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Kabob Plate', NULL, 15.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Kabob Plate', NULL, 17.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shrimp Kabob Plate', NULL, 16.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Veggie Kabob Plate', NULL, 14.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Kufta Kabob Plate', NULL, 16.00, 1, '[]', @now, @now),

-- === KIDS ===
(UUID(), @restaurant_id, @food_cat, 'Gyro Pizza', NULL, 7.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Gyro Kids', NULL, 7.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shawarma Kids Sandwich', NULL, 7.00, 1, '[]', @now, @now),

-- === SIDES ===
(UUID(), @restaurant_id, @food_cat, 'French Fries', NULL, 4.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Plain Pita', NULL, 1.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Bag of Pitas', NULL, 6.00, 1, '["Vegetarian"]', @now, @now),

-- === DESSERTS ===
(UUID(), @restaurant_id, @food_cat, 'Walnut Baklava', NULL, 3.50, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Pistachio Baklava', NULL, 4.00, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Hareeseh', NULL, 3.00, 1, '["Vegetarian"]', @now, @now);
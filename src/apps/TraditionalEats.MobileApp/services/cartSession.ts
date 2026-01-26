import AsyncStorage from '@react-native-async-storage/async-storage';

const CART_SESSION_ID_KEY = 'cart_session_id';

class CartSessionService {
  /**
   * Gets or creates a cart session ID.
   * Session ID is stored in AsyncStorage and persists across app restarts.
   */
  async getOrCreateSessionId(): Promise<string> {
    try {
      // Try to get existing session ID
      const existingSessionId = await AsyncStorage.getItem(CART_SESSION_ID_KEY);
      
      if (existingSessionId && this.isValidSessionId(existingSessionId)) {
        return existingSessionId;
      }

      // Generate new session ID (GUID v4)
      const newSessionId = this.generateGuid();
      await AsyncStorage.setItem(CART_SESSION_ID_KEY, newSessionId);
      return newSessionId;
    } catch (error) {
      console.error('Error getting/creating cart session ID:', error);
      // Fallback: generate a new one (won't persist, but will work for this session)
      return this.generateGuid();
    }
  }

  /**
   * Clears the cart session ID (e.g., on logout or cart merge)
   */
  async clearSessionId(): Promise<void> {
    try {
      await AsyncStorage.removeItem(CART_SESSION_ID_KEY);
    } catch (error) {
      // Silently fail
    }
  }

  /**
   * Gets the current session ID without creating a new one
   */
  async getSessionId(): Promise<string | null> {
    try {
      return await AsyncStorage.getItem(CART_SESSION_ID_KEY);
    } catch (error) {
      console.error('Error getting cart session ID:', error);
      return null;
    }
  }

  /**
   * Validates that a session ID is a valid GUID format
   */
  private isValidSessionId(sessionId: string | null): boolean {
    if (!sessionId) return false;
    // GUID format: 8-4-4-4-12 hex characters
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidRegex.test(sessionId);
  }

  /**
   * Generates a GUID v4
   */
  private generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}

export const cartSessionService = new CartSessionService();

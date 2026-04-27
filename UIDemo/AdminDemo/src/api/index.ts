import { MockApiService } from './mockApi';
import { RealApiService } from './realApi';

// Toggle this to switch between Mock and Real API
// You can also use: const isMock = import.meta.env.DEV;
const isMock = false; 

export const api = isMock ? new MockApiService() : new RealApiService();

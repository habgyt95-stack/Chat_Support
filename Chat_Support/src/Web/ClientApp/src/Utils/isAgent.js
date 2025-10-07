// utils/isAgent.js
export function isAgent(user) {
  if (!user) return false;
  return user.roles?.includes('Agent') || user.roles?.includes('Admin');
}

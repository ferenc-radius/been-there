// Helper function for deleting routes with CSRF token
export async function deleteRoute(routeId) {
  try {
    // Get CSRF token from the page
    const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenElement?.value || '';

    const response = await fetch(`/api/routes/${routeId}`, {
      method: 'DELETE',
      headers: {
        'RequestVerificationToken': token,
        'Content-Type': 'application/json'
      },
      credentials: 'same-origin'
    });

    return response.status;
  } catch (error) {
    console.error('Delete route error:', error);
    return 0;
  }
}

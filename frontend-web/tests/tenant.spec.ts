import { test, expect } from '@playwright/test';

test.describe('Tenant Courses & Library Flows', () => {
  test.beforeEach(async ({ page }) => {
    // Mock the Dashboard API call
    await page.route('http://localhost:5053/courses/dashboard/*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          academies: {
            "qatenant": [
              {
                courseId: 'mock-course-id',
                courseName: 'Mock Course',
                progressPercentage: 50
              }
            ]
          }
        })
      });
    });

    // Mock the Redeem Code API call
    await page.route('http://localhost:5053/library/redeem', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true })
      });
    });

    // Mock the Get Course API call
    await page.route('http://localhost:5053/courses/*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'mock-course-id',
          name: 'Mock Course',
          sections: [
            {
              id: 'mod-1',
              title: 'Module 1',
              items: [
                {
                  id: 'item-1',
                  type: 'Video',
                  title: 'Intro Video',
                  url: 'https://example.com/video.mp4'
                }
              ]
            }
          ]
        })
      });
    });

    // Setup authenticated state with mock token
    await page.goto('http://qatenant.localhost:3000/login');
    await page.evaluate(() => {
      window.localStorage.setItem('auth_token', 'mock_token');
      window.localStorage.setItem('student_id', 'mock_student');
      window.localStorage.setItem('tenant_id', 'qatenant');
    });
    // Navigate to tenant dashboard
    await page.goto('http://qatenant.localhost:3000/');
  });

  test('User can see dashboard and redeem a code', async ({ page }) => {
    // Wait for the dashboard to load (the mocked API returns courses)
    await expect(page.locator('text=Mock Course')).toBeVisible();

    // Verify redemption block is visible
    await expect(page.locator('text=Got a physical code?')).toBeVisible();

    // Fill in a redemption code
    await page.fill('input[placeholder="Enter your code"]', 'MOCK-LIBRARY-CODE-123');
    
    // Click redeem
    const redeemBtn = page.locator('button:has-text("Redeem Code")');
    await redeemBtn.click();
    
    // Since API is mocked to return 200, we should see success message
    await expect(page.locator('text=Code redeemed successfully!')).toBeVisible();
  });

  test('User can navigate to a course and interact with player', async ({ page }) => {
    // Navigate directly to a mock course ID page
    await page.goto('http://qatenant.localhost:3000/courses/mock-course-id');

    // Wait for syllabus to load (mock API returns data)
    await expect(page.locator('h3:has-text("Syllabus")')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=Module 1')).toBeVisible();
    await expect(page.locator('text=Intro Video')).toBeVisible();
  });
});

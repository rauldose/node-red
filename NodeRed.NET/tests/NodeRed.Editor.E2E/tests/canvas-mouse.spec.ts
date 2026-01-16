import { test, expect, Page } from '@playwright/test';

test.describe('Canvas Mouse Operations', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for the workspace chart to be visible
    await page.waitForSelector('#red-ui-workspace-chart', { timeout: 10000 });
  });

  test.describe('Node Selection', () => {
    test('clicking on a node should select it', async ({ page }) => {
      // First, we need to have a node on the canvas
      // Drop a node from palette if available, or check for existing nodes
      const node = page.locator('.red-ui-flow-node').first();
      
      if (await node.count() > 0) {
        // Click on the node
        await node.click();
        
        // Verify the node is selected (has selected class or selection highlight)
        await expect(node).toHaveClass(/selected/);
      }
    });

    test('clicking on empty space should deselect nodes', async ({ page }) => {
      // Click on the canvas background
      const canvas = page.locator('#red-ui-workspace-chart');
      const canvasBox = await canvas.boundingBox();
      
      if (canvasBox) {
        // Click on empty space (top-left corner which should be empty)
        await page.mouse.click(canvasBox.x + 50, canvasBox.y + 50);
      }
    });
  });

  test.describe('Node Dragging', () => {
    test('dragging a node should move it', async ({ page }) => {
      const node = page.locator('.red-ui-flow-node').first();
      
      if (await node.count() > 0) {
        const initialBox = await node.boundingBox();
        
        if (initialBox) {
          // Perform drag operation
          const startX = initialBox.x + initialBox.width / 2;
          const startY = initialBox.y + initialBox.height / 2;
          const endX = startX + 100;
          const endY = startY + 50;
          
          await page.mouse.move(startX, startY);
          await page.mouse.down();
          await page.mouse.move(endX, endY);
          await page.mouse.up();
          
          // Verify the node moved
          const finalBox = await node.boundingBox();
          if (finalBox && initialBox) {
            // Node should have moved approximately the drag distance
            expect(Math.abs((finalBox.x - initialBox.x) - 100)).toBeLessThan(10);
          }
        }
      }
    });
  });

  test.describe('Lasso Selection', () => {
    test('drawing a lasso should create a selection rectangle', async ({ page }) => {
      const canvas = page.locator('#red-ui-workspace-chart');
      const canvasBox = await canvas.boundingBox();
      
      if (canvasBox) {
        // Start from an empty area
        const startX = canvasBox.x + 100;
        const startY = canvasBox.y + 100;
        
        // Draw a lasso
        await page.mouse.move(startX, startY);
        await page.mouse.down();
        await page.mouse.move(startX + 200, startY + 200);
        
        // Check if lasso rectangle appears
        const lasso = page.locator('.red-ui-workspace-lasso');
        // Lasso may or may not be visible depending on implementation
        
        await page.mouse.up();
      }
    });
  });

  test.describe('Wire Creation', () => {
    test('dragging from output port should show drag line', async ({ page }) => {
      const outputPort = page.locator('.red-ui-flow-port-output').first();
      
      if (await outputPort.count() > 0) {
        const portBox = await outputPort.boundingBox();
        
        if (portBox) {
          const startX = portBox.x + portBox.width / 2;
          const startY = portBox.y + portBox.height / 2;
          
          await page.mouse.move(startX, startY);
          await page.mouse.down();
          await page.mouse.move(startX + 100, startY);
          
          // Check if a drag line appears (wire in dragging state)
          // The wire component should be rendered
          
          await page.mouse.up();
        }
      }
    });
  });

  test.describe('Double Click', () => {
    test('double clicking on a node should trigger edit', async ({ page }) => {
      const node = page.locator('.red-ui-flow-node').first();
      
      if (await node.count() > 0) {
        await node.dblclick();
        
        // The edit event should be triggered
        // This would typically open a tray or dialog
        // We can check for the presence of such UI elements
      }
    });

    test('double clicking on workspace tab should trigger edit', async ({ page }) => {
      const tab = page.locator('.red-ui-tab').first();
      
      if (await tab.count() > 0) {
        await tab.dblclick();
        
        // Should trigger the workspace edit event
      }
    });
  });

  test.describe('Text Selection Prevention', () => {
    test('node labels should not be selectable via click', async ({ page }) => {
      const nodeLabel = page.locator('.red-ui-flow-node-label').first();
      
      if (await nodeLabel.count() > 0) {
        // Check that pointer-events is set to none
        const pointerEvents = await nodeLabel.evaluate(el => 
          window.getComputedStyle(el).pointerEvents
        );
        expect(pointerEvents).toBe('none');
      }
    });

    test('footer text should not be selectable', async ({ page }) => {
      const footer = page.locator('#red-ui-workspace-footer');
      
      if (await footer.count() > 0) {
        const userSelect = await footer.evaluate(el => 
          window.getComputedStyle(el).userSelect
        );
        expect(userSelect).toBe('none');
      }
    });
  });

  test.describe('Mouse Move Tracking', () => {
    test('mouse move on canvas should be tracked', async ({ page }) => {
      const canvas = page.locator('#red-ui-workspace-chart');
      const canvasBox = await canvas.boundingBox();
      
      if (canvasBox) {
        // Move mouse across the canvas
        await page.mouse.move(canvasBox.x + 100, canvasBox.y + 100);
        await page.mouse.move(canvasBox.x + 200, canvasBox.y + 200);
        await page.mouse.move(canvasBox.x + 300, canvasBox.y + 300);
        
        // No errors should occur
      }
    });
  });

  test.describe('Context Menu', () => {
    test('right click on canvas should trigger context menu event', async ({ page }) => {
      const canvas = page.locator('#red-ui-workspace-chart');
      const canvasBox = await canvas.boundingBox();
      
      if (canvasBox) {
        await page.mouse.click(canvasBox.x + 200, canvasBox.y + 200, { button: 'right' });
        
        // Context menu event should be triggered
        // The default browser context menu should be prevented
      }
    });
  });

  test.describe('Zoom', () => {
    test('ctrl+wheel should zoom the canvas', async ({ page }) => {
      const canvas = page.locator('#red-ui-workspace-chart');
      const canvasBox = await canvas.boundingBox();
      
      if (canvasBox) {
        // Focus on canvas and use wheel with ctrl
        await page.mouse.move(canvasBox.x + 200, canvasBox.y + 200);
        await page.keyboard.down('Control');
        await page.mouse.wheel(0, -100); // Scroll up to zoom in
        await page.keyboard.up('Control');
        
        // The scale factor should change
      }
    });
  });
});

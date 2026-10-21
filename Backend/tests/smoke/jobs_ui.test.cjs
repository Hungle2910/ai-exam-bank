// Browser regressions for the existing dashboard. See tests/README.md for setup.
const assert = require('node:assert/strict');
const { before, after, test } = require('node:test');
const { chromium } = require('playwright');

const baseUrl = process.env.JOBS_UI_BASE_URL || 'http://localhost:5000';
const jobs = [
    { jobId: '11111111-1111-1111-1111-111111111111', jobType: 'ExamGeneration', status: 'Completed' },
    { jobId: '22222222-2222-2222-2222-222222222222', jobType: 'CsvImport', status: 'Completed' }
];
let browser;
before(async () => {
    browser = process.env.PLAYWRIGHT_CDP_URL
        ? await chromium.connectOverCDP(process.env.PLAYWRIGHT_CDP_URL)
        : await chromium.launch();
});
after(async () => { await browser?.close(); });

async function dashboard(t, { hidden = false, viewport = { width: 1280, height: 900 } } = {}) {
    const context = await browser.newContext({ viewport });
    t.after(() => context.close());
    const page = await context.newPage();
    const requests = [];
    await page.route('**/api/v1/jobs**', async route => {
        const request = route.request();
        requests.push(request);
        const isCreate = request.method() === 'POST';
        await route.fulfill({
            status: isCreate ? 202 : 200,
            contentType: 'application/json',
            body: JSON.stringify(request.url().includes('/dev-token') ? { token: 'synthetic-ui-token' }
                : isCreate ? { jobId: jobs[0].jobId } : jobs)
        });
    });
    // Control initial visibility before app.js runs; later changes use its real event handler.
    await page.addInitScript(initiallyHidden => {
        let hidden = initiallyHidden;
        Object.defineProperty(document, 'hidden', { get: () => hidden });
        window.changeTestVisibility = value => {
            hidden = value;
            document.dispatchEvent(new Event('visibilitychange'));
        };
        window.intervalsCreated = 0;
        const setInterval = window.setInterval;
        window.setInterval = (...args) => {
            window.intervalsCreated++;
            return setInterval(...args);
        };
    }, hidden);
    await page.goto(`${baseUrl}/jobs/`);
    return { page, requests };
}

async function badgeTexts(page) {
    await page.waitForSelector('.job-type-badge');
    return page.locator('.job-type-badge').allTextContents();
}

test('badges and case-insensitive search use each job type', async t => {
    const { page } = await dashboard(t);
    assert.deepEqual(await badgeTexts(page), ['⚙️ ExamGeneration', '⚙️ CsvImport']);
    for (const [query, badge] of [['examGENERATION', '⚙️ ExamGeneration'], ['csvIMPORT', '⚙️ CsvImport']]) {
        await page.locator('#searchInput').fill(query);
        assert.deepEqual(await badgeTexts(page), [badge]);
    }
    await page.locator('#searchInput').fill('unmatched-type');
    assert.equal(await page.locator('.job-type-badge').count(), 0);
    await page.locator('#searchInput').fill('');
    assert.equal((await badgeTexts(page)).length, 2);
});

test('initially hidden dashboard waits for visibility before fetching or polling', async t => {
    const { page, requests } = await dashboard(t, { hidden: true });
    await page.clock.install();
    await page.clock.fastForward(20_000);
    assert.equal(requests.length, 0);
    assert.equal(await page.evaluate(() => window.intervalsCreated), 0);

    await page.evaluate(() => window.changeTestVisibility(false));
    await badgeTexts(page);
    const afterShowing = requests.length;
    await Promise.all([
        page.waitForResponse(response => response.url().includes('scopeResourceId=')),
        page.clock.fastForward(5_000)
    ]);
    assert.ok(requests.length > afterShowing, 'visible dashboard must resume periodic requests');

    await page.evaluate(() => window.changeTestVisibility(true));
    const afterHiding = requests.length;
    await page.clock.fastForward(20_000);
    assert.equal(requests.length, afterHiding, 'hiding must stop polling again');
});

for (const viewport of [{ width: 800, height: 320 }, { width: 375, height: 400 }, { width: 1280, height: 900 }]) {
    test(`create form remains reachable and submits at ${viewport.width}x${viewport.height}`, async t => {
        const { page, requests } = await dashboard(t, { viewport });
        await badgeTexts(page);
        await page.locator('#btnCreateJobModal').click();
        await page.locator('#modalJobType').selectOption('CsvImport');
        await page.locator('#modalPayloadRef').fill('synthetic-ui-payload');
        await page.locator('#modalIdempotencyKey').fill('synthetic-ui-key');
        const modal = page.locator('.modal-content');
        await modal.evaluate(element => element.getAnimations().forEach(animation => animation.finish()));
        const box = await modal.boundingBox();
        assert.ok(box.y >= 0, 'the beginning of the form must be accessible');
        if (viewport.height === 900) {
            assert.ok(Math.abs(box.y - (viewport.height - box.height) / 2) < 2,
                'a fitting form must stay vertically centered');
        } else {
            assert.ok(box.height > viewport.height, 'short viewport must exercise overflow');
        }
        const submit = page.locator('#btnSubmitCreateJob');
        await page.locator('#createJobModal').evaluate(element => { element.scrollTop = element.scrollHeight; });
        const submitBox = await submit.boundingBox();
        assert.ok(submitBox.y >= 0 && submitBox.y + submitBox.height <= viewport.height,
            `the entire submit button must be visible after scrolling: ${JSON.stringify(submitBox)}`);
        await submit.click();
        await page.waitForFunction(() => !document.getElementById('createJobModal').classList.contains('active'));
        const create = requests.find(request => request.method() === 'POST');
        assert.ok(create, 'form must send an enqueue request');
        assert.equal(create.postDataJSON().jobType, 'CsvImport');
        assert.equal(create.postDataJSON().payloadReference, 'synthetic-ui-payload');
    });
}

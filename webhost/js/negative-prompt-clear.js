
	// پاک کردن خودکار متن منفی در اولین کلیک
	const negativeTextarea = document.getElementById('negative_prompt');
	let isFirstFocus = true;

	negativeTextarea.addEventListener('focus', function() {
		if (isFirstFocus && this.value.trim() !== '') {
			this.value = '';
			isFirstFocus = false; // فقط یک بار اجرا بشه
		}
	});

	negativeTextarea.addEventListener('input', function() {
		isFirstFocus = false;
	});

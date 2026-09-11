mergeInto(LibraryManager.library, {
  SubmitGoogleForm: function (formUrlPtr, formBodyPtr) {
    var formUrl = UTF8ToString(formUrlPtr);
    var formBody = UTF8ToString(formBodyPtr);

    try {
      fetch(formUrl, {
        method: 'POST',
        mode: 'no-cors',
        credentials: 'omit',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: formBody
      }).catch(function (error) {
        console.error('Google Forms telemetry submission failed:', error);
      });
    } catch (error) {
      console.error('Google Forms telemetry submission failed:', error);
    }
  }
});

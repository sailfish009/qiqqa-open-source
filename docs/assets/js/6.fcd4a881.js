(window.webpackJsonp = window.webpackJsonp || []).push([
  [6],
  {
    166: function (t, e, n) {
      "use strict";
      var a = n(34);
      n.n(a).a;
    },
    183: function (t, e, n) {
      "use strict";
      n.r(e);
      var a = {
          functional: !0,
          props: {
            type: { type: String, default: "tip" },
            text: String,
            vertical: { type: String, default: "top" },
          },
          render: (t, { props: e, slots: n }) =>
            t(
              "span",
              {
                class: ["badge", e.type],
                style: { verticalAlign: e.vertical },
              },
              e.text || n().default
            ),
        },
        i = (n(166), n(0)),
        p = Object(i.a)(a, void 0, void 0, !1, null, "0a45b61c", null);
      e.default = p.exports;
    },
    34: function (t, e, n) {},
  },
]);